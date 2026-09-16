import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { Fragment, memo, useEffect, useMemo, useRef, useState } from "react";
import { Link, useParams } from "react-router";

import "@formio/js/dist/formio.form.min.css";

import { FormLoadError, SpecRejectedError } from "@/api/forms";
import type { FormValidationError } from "@/api/forms";
import { registerBcgovComponents } from "@/formio/bcgovComponents";
import { revealAllComponents } from "@/formio/specPreview";
import {
    flattenComponents,
    listEditableComponents,
    setLabel,
    setRequired,
    type EditableComponent,
} from "@/formio/specEdit";
import {
    findDuplicateKeys,
    findUnknownConditionalTargets,
} from "@/formio/specRules";
import { useDraft, usePublishForm, useSaveDraft } from "@/hooks/useForms";
import { paths } from "@/routes/paths";
import RatesPanel from "./RatesPanel";
import homeStyles from "./HomePage.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormEditorPage.module.css";

// The one form the eligibility rate table applies to; its rates show below the
// editor only when this form is open.
const ESTIMATOR_FORM_SPEC_ID = "eligibility-estimator";

// Register the custom bcgovAccordion type so the preview renders it as a real
// component rather than a blank slot when a spec includes one. Idempotent.
registerBcgovComponents();

/** A reason a save/publish was refused; `field` links it to a component row. */
interface EditorError {
    message: string;
    field?: string;
}

// Client-side mirror of the cheap publish rules, so an obvious problem shows
// before the round-trip. The server stays the authoritative gate on publish.
function ruleErrors(spec: FormType): EditorError[] {
    const errors: EditorError[] = [];
    for (const key of findDuplicateKeys(spec)) {
        errors.push({ message: `Duplicate field key: ${key}`, field: key });
    }
    for (const key of findUnknownConditionalTargets(spec)) {
        errors.push({
            message: `A condition refers to a field that doesn't exist: ${key}`,
        });
    }
    return errors;
}

// Move focus and scroll to the settings row an error belongs to. A miss is
// silent — the message is already on screen.
function focusRow(field: string): void {
    const input = document.querySelector<HTMLElement>(
        `input[aria-label="Label for ${CSS.escape(field)}"]`,
    );
    input?.focus();
    input?.scrollIntoView({ block: "center", behavior: "smooth" });
}

// One rendered field, read-only. Rendered on its own so each field can sit in
// the same grid row as its settings. Memoized on the component's content so
// only the field being edited re-initialises Form.io, not the whole form.
const PreviewCell = memo(
    function PreviewCell({ component }: { component: Record<string, unknown> }) {
        const src = useMemo(
            () =>
                ({
                    display: "form",
                    components: [component],
                }) as unknown as FormType,
            [component],
        );
        return <Form src={src} options={{ readOnly: true, noAlerts: true }} />;
    },
    (a, b) => JSON.stringify(a.component) === JSON.stringify(b.component),
);

// The accessible error summary: one list of every reason, focused when it
// appears so a screen-reader user is told, not left at the button in silence.
// Each reason that maps to a field links to its settings row.
function EditorErrorSummary({ errors }: { errors: EditorError[] }) {
    const headingRef = useRef<HTMLHeadingElement>(null);
    const signal = errors.map((e) => e.message).join("|");
    useEffect(() => {
        headingRef.current?.focus();
    }, [signal]);

    return (
        <div className={styles.errorSummary} role="alert">
            <h2 ref={headingRef} tabIndex={-1} className={styles.errorTitle}>
                There is a problem
            </h2>
            <ul className={styles.errorList}>
                {errors.map((error, index) => {
                    const field = error.field;
                    return (
                        <li key={`${field ?? ""}:${error.message}:${index}`}>
                            {field ? (
                                <button
                                    type="button"
                                    className={styles.errorLink}
                                    onClick={() => focusRow(field)}
                                >
                                    {error.message}
                                </button>
                            ) : (
                                error.message
                            )}
                        </li>
                    );
                })}
            </ul>
        </div>
    );
}

// Opens one form: each field is rendered read-only (the real BC Gov skin) in the
// same row as a panel for editing its label and required flag. Edits update a
// working copy so the row re-renders live; Save draft stores it, Publish
// releases it as the next version.
export default function FormEditorPage() {
    const { formSpecId } = useParams<{ formSpecId: string }>();
    const { data: draft, isPending, error } = useDraft(formSpecId);
    const saveDraft = useSaveDraft(formSpecId);
    const publish = usePublishForm(formSpecId);

    // The edited copy the panel drives and Save draft/Publish send. It is seeded
    // from the loaded draft once per form (keyed on formSpecId) by adjusting
    // state during render — the sanctioned alternative to seeding in an effect.
    // Keying on the form, not the draft object, means a background refetch (e.g.
    // after a save) never re-seeds and discards in-progress edits; only opening a
    // different form does. The loaded draft stays untouched; the setters return a
    // new spec each edit.
    const [workingSpec, setWorkingSpec] = useState<FormType | null>(null);
    const [seededFor, setSeededFor] = useState<string | undefined>(undefined);
    const [dirty, setDirty] = useState(false);
    if (seededFor !== formSpecId) {
        if (draft) {
            setSeededFor(formSpecId);
            setWorkingSpec(draft.spec);
            setDirty(false);
        } else if (workingSpec !== null) {
            setWorkingSpec(null);
        }
    }

    // Preview shows every component at once (conditionals stripped), flattened to
    // one field per row so each lines up with its settings.
    const rows = useMemo(
        () =>
            workingSpec
                ? flattenComponents(revealAllComponents(workingSpec))
                : [],
        [workingSpec],
    );
    const editableByKey = useMemo(() => {
        const map = new Map<string, EditableComponent>();
        if (workingSpec) {
            for (const component of listEditableComponents(workingSpec)) {
                map.set(component.key, component);
            }
        }
        return map;
    }, [workingSpec]);

    const [clientErrors, setClientErrors] = useState<EditorError[]>([]);
    const [lastAction, setLastAction] = useState<"save" | "publish" | null>(
        null,
    );

    // A server refusal (422) from save or publish, surfaced in the summary;
    // client-side rule errors take precedence when present.
    // The error for the current action: a publish runs save then publish, so it
    // can come from either; a save only from save. Scoped by lastAction so a
    // stale error from a previous action doesn't linger.
    const activeError =
        lastAction === "publish"
            ? (publish.error ?? saveDraft.error)
            : lastAction === "save"
              ? saveDraft.error
              : null;
    // A 422 carries a field-error list; any other refusal (a 502 from the
    // content engine, a network drop) carries none, so fall back to a plain
    // message with the status rather than failing silently.
    const rejection =
        activeError instanceof SpecRejectedError ? activeError : null;
    const serverErrors: EditorError[] = rejection
        ? rejection.errors.map((e: FormValidationError) => ({
              message: e.message,
              field: e.field,
          }))
        : [];
    const shownErrors: EditorError[] = clientErrors.length
        ? clientErrors
        : serverErrors.length
          ? serverErrors
          : activeError
            ? [
                  {
                      message: `Could not ${
                          lastAction === "publish" ? "publish" : "save"
                      } this form${
                          rejection ? ` (error ${rejection.status})` : ""
                      }. Please try again.`,
                  },
              ]
            : [];
    const busy = saveDraft.isPending || publish.isPending;

    function editLabel(key: string, value: string) {
        setWorkingSpec((s) => (s ? setLabel(s, key, value) : s));
        setDirty(true);
    }

    function editRequired(key: string, required: boolean) {
        setWorkingSpec((s) => (s ? setRequired(s, key, required) : s));
        setDirty(true);
    }

    function handleSave() {
        if (!workingSpec) return;
        setLastAction("save");
        const errs = ruleErrors(workingSpec);
        setClientErrors(errs);
        if (errs.length) return;
        saveDraft.mutate(
            { spec: workingSpec, title: draft?.title ?? null },
            { onSuccess: () => setDirty(false) },
        );
    }

    async function handlePublish() {
        if (!workingSpec) return;
        setLastAction("publish");
        const errs = ruleErrors(workingSpec);
        setClientErrors(errs);
        if (errs.length) return;
        try {
            // Persist the working copy, then publish that draft as the next
            // version. A 422 from either call surfaces via its mutation error.
            await saveDraft.mutateAsync({
                spec: workingSpec,
                title: draft?.title ?? null,
            });
            await publish.mutateAsync();
            setDirty(false);
        } catch {
            // Handled through the error summary; the working copy is untouched.
        }
    }

    return (
        <div className={homeStyles.page}>
            <nav aria-label="Breadcrumb">
                <Link to={paths.adminFormManagement}>← Form management</Link>
            </nav>

            <header className={adminStyles.heading}>
                <p className={adminStyles.eyebrow}>My Self Serve</p>
                <h1>{draft?.title?.trim() ? draft.title : formSpecId}</h1>
            </header>

            {isPending && <p>Loading form…</p>}

            {!isPending && error && (
                <p role="alert">
                    {error instanceof FormLoadError && error.status === 404
                        ? "This form could not be found — check the address."
                        : `Could not load this form: ${error.message}`}
                </p>
            )}

            {!isPending && !error && workingSpec && (
                <>
                    <div className={styles.grid}>
                        <div className={styles.headCell}>Preview</div>
                        <div className={styles.headCell}>Field settings</div>

                        {rows.map((component, index) => {
                            const key =
                                typeof component.key === "string"
                                    ? component.key
                                    : `row-${index}`;
                            const editable = editableByKey.get(key);
                            return (
                                <Fragment key={key}>
                                    <div className={styles.previewCell}>
                                        <PreviewCell component={component} />
                                    </div>
                                    <div className={styles.editCell}>
                                        {editable ? (
                                            <div className={styles.editControls}>
                                                <code className={styles.key}>
                                                    {editable.key}
                                                </code>
                                                <label
                                                    className={styles.labelField}
                                                >
                                                    <span
                                                        className={
                                                            styles.fieldLabel
                                                        }
                                                    >
                                                        Label
                                                    </span>
                                                    <input
                                                        type="text"
                                                        value={editable.label}
                                                        aria-label={`Label for ${editable.key}`}
                                                        onChange={(e) =>
                                                            editLabel(
                                                                editable.key,
                                                                e.target.value,
                                                            )
                                                        }
                                                    />
                                                </label>
                                                <label
                                                    className={
                                                        styles.requiredField
                                                    }
                                                >
                                                    <input
                                                        type="checkbox"
                                                        checked={
                                                            editable.required
                                                        }
                                                        aria-label={`Required: ${editable.key}`}
                                                        onChange={(e) =>
                                                            editRequired(
                                                                editable.key,
                                                                e.target.checked,
                                                            )
                                                        }
                                                    />
                                                    Required
                                                </label>
                                            </div>
                                        ) : (
                                            <span
                                                className={styles.noEdit}
                                                aria-hidden="true"
                                            >
                                                —
                                            </span>
                                        )}
                                    </div>
                                </Fragment>
                            );
                        })}
                    </div>

                    {shownErrors.length > 0 && (
                        <EditorErrorSummary errors={shownErrors} />
                    )}

                    <div className={styles.saveBar}>
                        <button
                            type="button"
                            className={styles.saveButton}
                            onClick={handleSave}
                            disabled={busy}
                        >
                            {saveDraft.isPending && lastAction === "save"
                                ? "Saving…"
                                : "Save draft"}
                        </button>
                        <button
                            type="button"
                            className={styles.publishButton}
                            onClick={handlePublish}
                            disabled={busy}
                        >
                            {busy && lastAction === "publish"
                                ? "Publishing…"
                                : "Publish"}
                        </button>
                        {lastAction === "save" &&
                            saveDraft.isSuccess &&
                            !dirty &&
                            clientErrors.length === 0 && (
                                <span role="status" className={styles.saved}>
                                    Draft saved.
                                </span>
                            )}
                        {lastAction === "publish" &&
                            publish.isSuccess &&
                            !dirty &&
                            publish.data && (
                                <span role="status" className={styles.saved}>
                                    Published version {publish.data.version}.
                                </span>
                            )}
                    </div>

                    {formSpecId === ESTIMATOR_FORM_SPEC_ID && <RatesPanel />}
                </>
            )}
        </div>
    );
}
