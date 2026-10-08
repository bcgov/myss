import { useEffect, useRef, useState } from "react";
import { Button, TextField } from "@bcgov/design-system-react-components";

import type { EligibilityRates } from "@/api/eligibility";
import { RatesRejectedError } from "@/api/eligibilityRatesAdmin";
import { useEstimatorRates } from "@/hooks/useEligibility";
import { useSaveEstimatorRates } from "@/hooks/useEstimatorRatesAdmin";
import {
    ASSET_COLUMNS,
    INCOME_COLUMN_GROUPS,
    INCOME_COLUMNS,
    isNotApplicable,
} from "@/lib/rateColumns";
import {
    applyDraft,
    assetCellId,
    cellLabel,
    hasChanges,
    incomeCellId,
    inlineMessage,
    isCompleteTable,
    isEditableCell,
    ratesDraft,
    validateDraft,
    type RatesDraft,
} from "./ratesEdit";
import styles from "./RatesPanel.module.css";

// The eligibility rate table the estimator computes against (income limits by
// client-type category A–I in three groups, asset ceilings A–D), with one Edit /
// Save / Cancel for both tables. A save publishes a table effective today.

const money = new Intl.NumberFormat("en-CA", {
    style: "currency",
    currency: "CAD",
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
});

/** One line of the error summary; `field` is set when it names an editable cell. */
interface SummaryError {
    field?: string;
    message: string;
}

/** An edit in progress: the typed text, and the table it started from. */
interface Edit {
    base: EligibilityRates;
    draft: RatesDraft;
}

type FocusTarget = "firstInput" | "editButton" | "summary";

const CHANGED_SINCE_EDIT =
    "The rates changed since you started editing. Cancel to see the current rates, then make your changes again.";

/** A couple column at family size 1 has no limit; read out in full by screen readers. */
function NotApplicable() {
    return (
        <>
            <span aria-hidden="true">N/A</span>
            <span className={styles.visuallyHidden}>Not applicable</span>
        </>
    );
}

function describeFailure(error: Error): SummaryError[] {
    if (!(error instanceof RatesRejectedError)) {
        return [
            {
                message:
                    "The save could not be confirmed. The rates may not have been saved. Your changes are still here: try again, or cancel and check the table.",
            },
        ];
    }
    if (error.errors.length > 0) {
        return error.errors.map(({ field, message }) =>
            isEditableCell(field) ? { field, message } : { message },
        );
    }
    if (error.status === 401) {
        return [{ message: "Could not save the rates: you are no longer signed in (error 401). Sign in again, then make your changes again." }];
    }
    if (error.status === 403) {
        return [{ message: "Could not save the rates: your account is not allowed to change them (error 403)." }];
    }
    if (error.status >= 500) {
        return [
            {
                message: `The save could not be confirmed (error ${error.status}). The rates may not have been saved. Your changes are still here: try again, or cancel and check the table.`,
            },
        ];
    }
    return [{ message: `Could not save the rates (error ${error.status}). Your changes are still here: try again, or cancel.` }];
}

export default function RatesPanel() {
    const { data: rates, isPending, error } = useEstimatorRates();
    const save = useSaveEstimatorRates();
    const [edit, setEdit] = useState<Edit | null>(null);
    const [errors, setErrors] = useState<SummaryError[]>([]);
    const [statusMessage, setStatusMessage] = useState("");
    const panelRef = useRef<HTMLElement>(null);
    const summaryHeadingRef = useRef<HTMLHeadingElement>(null);
    const pendingFocus = useRef<FocusTarget | null>(null);

    // Moves focus once the render that shows its target has happened.
    useEffect(() => {
        const target = pendingFocus.current;
        if (!target) return;
        pendingFocus.current = null;
        if (target === "summary") {
            summaryHeadingRef.current?.focus();
        } else if (target === "firstInput") {
            panelRef.current?.querySelector<HTMLInputElement>("input")?.focus();
        } else {
            panelRef.current?.querySelector<HTMLButtonElement>("[data-rates-edit]")?.focus();
        }
    });

    const cellMessages = new Map(errors.flatMap(({ field, message }) => (field ? [[field, message] as const] : [])));

    function startEditing() {
        if (!rates) return;
        setEdit({ base: rates, draft: ratesDraft(rates) });
        setErrors([]);
        setStatusMessage("");
        pendingFocus.current = "firstInput";
    }

    function finishEditing(message: string) {
        setEdit(null);
        setErrors([]);
        setStatusMessage(message);
        pendingFocus.current = "editButton";
    }

    function showErrors(summary: SummaryError[]) {
        setErrors(summary);
        pendingFocus.current = "summary";
    }

    function cancel() {
        save.reset();
        finishEditing("");
    }

    function saveDraft() {
        if (!edit) return;

        const invalid = validateDraft(edit.draft);
        const invalidIds = Object.keys(invalid);
        if (invalidIds.length > 0) {
            showErrors(invalidIds.map((field) => ({ field, message: invalid[field] })));
            return;
        }

        // A refetch replaced the table after Edit; saving the draft would undo that change.
        if (rates !== edit.base) {
            showErrors([{ message: CHANGED_SINCE_EDIT }]);
            return;
        }

        if (!hasChanges(edit.base, edit.draft)) {
            finishEditing("No changes to save.");
            return;
        }

        setErrors([]);
        save.mutate(applyDraft(edit.draft), {
            onSuccess: (saved) =>
                finishEditing(
                    `Eligibility rates saved. The estimator now uses the rates effective ${saved.effectiveDate}.`,
                ),
            onError: (failure) => showErrors(describeFailure(failure)),
        });
    }

    function focusCell(field: string) {
        const input = panelRef.current?.querySelector<HTMLInputElement>(
            `input[name="${CSS.escape(field)}"]`,
        );
        input?.focus({ preventScroll: true });
        input?.scrollIntoView({ block: "center" });
    }

    function amountCell(id: string, amount: number) {
        if (!edit) return money.format(amount);
        const message = cellMessages.get(id);
        return (
            <div className={styles.cellInput}>
                <TextField
                    size="small"
                    name={id}
                    aria-label={cellLabel(id)}
                    inputMode="decimal"
                    value={edit.draft[id] ?? ""}
                    onChange={(text) =>
                        setEdit((current) => current && { ...current, draft: { ...current.draft, [id]: text } })
                    }
                    isReadOnly={save.isPending}
                    isInvalid={message !== undefined}
                    errorMessage={message === undefined ? undefined : inlineMessage(message)}
                />
            </div>
        );
    }

    return (
        <section ref={panelRef} aria-labelledby="rates-heading" className={styles.panel}>
            <h2 id="rates-heading" className={styles.heading}>
                Eligibility rates
            </h2>
            <p className={styles.caption}>
                Rate changes are saved separately from the form and take effect in
                the estimator as soon as they are saved.
            </p>

            {isPending && <p>Loading rates…</p>}

            {!rates && error && (
                <p role="alert" className={styles.error}>
                    Could not load the eligibility rates: {error.message}
                </p>
            )}

            {rates && (
                <>
                    <p className={styles.effective}>
                        Effective {rates.effectiveDate}
                    </p>

                    {error && (
                        <p className={styles.notice}>
                            The rates could not be refreshed, so they may be out of date: {error.message}
                        </p>
                    )}

                    <p role="status" className={styles.saved}>
                        {statusMessage}
                    </p>

                    {errors.length > 0 && (
                        <div className={styles.errorSummary} role="alert">
                            <h3
                                ref={summaryHeadingRef}
                                tabIndex={-1}
                                className={styles.errorTitle}
                            >
                                There is a problem
                            </h3>
                            <ul className={styles.errorList}>
                                {errors.map(({ field, message }, index) => (
                                    <li key={`${field ?? ""}:${index}`}>
                                        {field ? (
                                            <button
                                                type="button"
                                                className={styles.errorLink}
                                                onClick={() => focusCell(field)}
                                            >
                                                {cellLabel(field)}: {message}
                                            </button>
                                        ) : (
                                            message
                                        )}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}

                    <h3 id="income-limits-heading" className={styles.tableTitle}>
                        Monthly income limits
                    </h3>
                    <p className={styles.caption}>By client-type category</p>
                    <div className={styles.tableWrap}>
                        <table
                            aria-labelledby="income-limits-heading"
                            className={styles.table}
                        >
                            <colgroup>
                                <col />
                            </colgroup>
                            {INCOME_COLUMN_GROUPS.map(({ group, span }) => (
                                <colgroup key={group} span={span} />
                            ))}
                            <thead>
                                <tr>
                                    <th
                                        rowSpan={2}
                                        scope="col"
                                        className={styles.rowHeading}
                                    >
                                        Family size
                                    </th>
                                    {INCOME_COLUMN_GROUPS.map(({ group, span }) => (
                                        <th
                                            key={group}
                                            colSpan={span}
                                            scope="colgroup"
                                            className={styles.groupHeading}
                                        >
                                            {group}
                                        </th>
                                    ))}
                                </tr>
                                <tr>
                                    {INCOME_COLUMNS.map(({ letter }) => (
                                        <th key={letter} scope="col">
                                            {letter.toUpperCase()}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {rates.incomeRows.map((row) => (
                                    <tr key={row.familySize}>
                                        <th scope="row">{row.familySize}</th>
                                        {INCOME_COLUMNS.map(({ letter }) => (
                                            <td key={letter}>
                                                {isNotApplicable(row.familySize, letter) ? (
                                                    <NotApplicable />
                                                ) : (
                                                    amountCell(incomeCellId(row.familySize, letter), row[letter])
                                                )}
                                            </td>
                                        ))}
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    <h3 id="asset-limits-heading" className={styles.tableTitle}>
                        Asset limits
                    </h3>
                    <div className={styles.tableWrap}>
                        <table
                            aria-labelledby="asset-limits-heading"
                            className={styles.table}
                        >
                            <thead>
                                <tr>
                                    {ASSET_COLUMNS.map((letter) => (
                                        <th key={letter} scope="col">
                                            {letter.toUpperCase()}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                <tr>
                                    {ASSET_COLUMNS.map((letter) => (
                                        <td key={letter}>
                                            {amountCell(assetCellId(letter), rates.assetLimits[letter])}
                                        </td>
                                    ))}
                                </tr>
                            </tbody>
                        </table>
                    </div>

                    <div className={styles.actions}>
                        {edit && (
                            <>
                                <Button variant="primary" isPending={save.isPending} onPress={saveDraft}>
                                    Save rates
                                </Button>
                                <Button variant="secondary" isDisabled={save.isPending} onPress={cancel}>
                                    Cancel
                                </Button>
                            </>
                        )}
                        {!edit && isCompleteTable(rates) && (
                            <Button variant="primary" data-rates-edit onPress={startEditing}>
                                Edit rates
                            </Button>
                        )}
                        {!edit && !isCompleteTable(rates) && (
                            <p className={styles.notice}>
                                This rate table does not have one row for each family size from 1 to 7, so it cannot be edited here.
                            </p>
                        )}
                    </div>
                </>
            )}
        </section>
    );
}
