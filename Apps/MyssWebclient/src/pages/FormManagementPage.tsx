import {
    AlertDialog,
    Button,
    Modal,
    Radio,
    RadioGroup,
    Select,
} from "@bcgov/design-system-react-components";
import { useEffect, useId, useRef, useState } from "react";
import type { SubmitEvent } from "react";
import { Link, useNavigate } from "react-router";

import { useAdminForms, useFreshFormList } from "@/hooks/useForms";
import {
    adminFormEditorPath,
    adminFormFromJsonPath,
    adminNewFormPath,
} from "@/routes/paths";
import type { FormSummary, FormVersionSummary } from "@/api/forms";
import { isValidFormId } from "@/lib/formSpecId";
import {
    confirmValue,
    describeSaveTarget,
    saveTargetFor,
    type ExistingFormTarget,
} from "@/lib/formVersions";
import FormJsonDialog from "@/widgets/admin/FormJsonDialog";
import homeStyles from "./HomePage.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormManagementPage.module.css";

// The admin forms list: every form with its versions and which one is
// published. Each row links into the editor, and each version opens its JSON.
export default function FormManagementPage() {
    const { data: forms, isPending, error } = useAdminForms();
    const [openVersion, setOpenVersion] = useState<{
        form: FormSummary;
        version: FormVersionSummary;
    } | null>(null);

    return (
        <div className={homeStyles.page}>
            <header className={adminStyles.heading}>
                <p className={adminStyles.eyebrow}>My Self Serve</p>
                <h1>Form management</h1>
            </header>

            {isPending && <p className={styles.status}>Loading forms…</p>}

            {!isPending && error && (
                <p role="alert" className={styles.error}>
                    Could not load forms: {error.message}
                </p>
            )}

            {!isPending && !error && forms?.length === 0 && (
                <p className={styles.status}>No forms found.</p>
            )}

            {!isPending && !error && forms && forms.length > 0 && (
                <ul className={styles.list}>
                    {forms.map((form) => (
                        <FormRow
                            key={form.formSpecId}
                            form={form}
                            onOpenVersion={(version) =>
                                setOpenVersion({ form, version })
                            }
                        />
                    ))}
                </ul>
            )}

            {openVersion && (
                <FormJsonDialog
                    formSpecId={openVersion.form.formSpecId}
                    formName={displayName(openVersion.form)}
                    version={openVersion.version.version}
                    isPublished={openVersion.version.isPublished}
                    isOpen
                    onClose={() => setOpenVersion(null)}
                />
            )}

            {!isPending && !error && forms && (
                <NewFormSection forms={forms} />
            )}
        </div>
    );
}

/** The two ways to start: the editor's template, or pasted JSON. */
type PanelVariant = "template" | "json";

const PANEL_TEXT = {
    template: { heading: "New form", submit: "Create" },
    json: { heading: "Create form from JSON", submit: "Continue" },
} as const;

const INVALID_ID =
    "Enter a form ID using only lowercase letters, numbers and hyphens.";

/** What Create form from JSON makes: a version of a listed form, or a new one. */
type JsonKind = "existing" | "new";

/** The existing form that Create form from JSON would add a version to. */
interface ExistingFormChoice {
    formSpecId: string;
    formName: string;
    target: ExistingFormTarget;
}

// New form and Create form from JSON, side by side; only one panel is open
// at a time. Nothing is stored until the first save on the page they open.
function NewFormSection({ forms }: Readonly<{ forms: FormSummary[] }>) {
    const [open, setOpen] = useState<PanelVariant | null>(null);
    const templateButtonRef = useRef<HTMLButtonElement>(null);
    const jsonButtonRef = useRef<HTMLButtonElement>(null);
    const closedPanel = useRef<PanelVariant | null>(null);

    // Cancel removes the panel under the user's focus, so focus goes back to
    // the button that opened it.
    useEffect(() => {
        if (open !== null || closedPanel.current === null) return;
        const button =
            closedPanel.current === "json" ? jsonButtonRef : templateButtonRef;
        button.current?.focus();
        closedPanel.current = null;
    }, [open]);

    if (open) {
        return (
            <NewFormPanel
                variant={open}
                forms={forms}
                onCancel={() => {
                    closedPanel.current = open;
                    setOpen(null);
                }}
            />
        );
    }

    return (
        <div className={styles.openButtons}>
            <button
                ref={templateButtonRef}
                type="button"
                className={styles.newFormButton}
                onClick={() => setOpen("template")}
            >
                New form
            </button>
            <button
                ref={jsonButtonRef}
                type="button"
                className={styles.newFormButton}
                onClick={() => setOpen("json")}
            >
                Create form from JSON
            </button>
        </div>
    );
}

// Collects the form and title, reads the forms list again, and opens the
// editor or the JSON page, or the existing-form warning for a form in use.
function NewFormPanel({
    variant,
    forms,
    onCancel,
}: Readonly<{
    variant: PanelVariant;
    forms: FormSummary[];
    onCancel: () => void;
}>) {
    const navigate = useNavigate();
    const readForms = useFreshFormList();
    const headingId = useId();
    const titleHintId = useId();
    const idHintId = useId();
    const idErrorId = useId();
    // With no forms to choose from, a new form is the only answer that works.
    const [kind, setKind] = useState<JsonKind>(
        forms.length > 0 ? "existing" : "new",
    );
    const [chosen, setChosen] = useState<string | null>(null);
    const [formSpecId, setFormSpecId] = useState("");
    const [title, setTitle] = useState("");
    const [idError, setIdError] = useState<string | null>(null);
    const [choiceError, setChoiceError] = useState<string | null>(null);
    const [checkFailed, setCheckFailed] = useState(false);
    const [checking, setChecking] = useState(false);
    const [warning, setWarning] = useState<ExistingFormChoice | null>(null);
    const idInputRef = useRef<HTMLInputElement>(null);
    const selectRef = useRef<HTMLDivElement>(null);
    const submitRef = useRef<HTMLButtonElement>(null);
    // Counts checks; any edit, or closing the panel, makes a read still under
    // way stale, so its result is ignored.
    const checkRun = useRef(0);
    const leaving = useRef(false);

    // The panel opens only from its button, so this never steals focus on
    // page load. The JSON panel focuses its chosen radio button instead.
    useEffect(() => {
        if (variant === "template") idInputRef.current?.focus();
        return () => {
            checkRun.current += 1;
        };
    }, [variant]);

    function changed() {
        checkRun.current += 1;
        setChecking(false);
        setCheckFailed(false);
        setIdError(null);
        setChoiceError(null);
    }

    const pickingExisting = variant === "json" && kind === "existing";
    const chosenForm = forms.find((form) => form.formSpecId === chosen);
    const id = pickingExisting ? (chosen ?? "") : formSpecId.trim();

    function refuseId(message: string) {
        setIdError(message);
        idInputRef.current?.focus();
    }

    // Focus goes to the Select's button, which reads the error with it.
    function refuseChoice(message: string) {
        setChoiceError(message);
        selectRef.current?.querySelector("button")?.focus();
    }

    async function submit(event: SubmitEvent<HTMLFormElement>) {
        event.preventDefault();
        if (checking) return;
        if (pickingExisting && !chosen) {
            refuseChoice("Choose a form.");
            return;
        }
        if (!pickingExisting && !isValidFormId(id)) {
            refuseId(INVALID_ID);
            return;
        }
        setCheckFailed(false);
        setChecking(true);
        const run = ++checkRun.current;
        let fresh: FormSummary[];
        try {
            fresh = await readForms();
        } catch {
            if (run === checkRun.current) {
                setChecking(false);
                setCheckFailed(true);
            }
            return;
        }
        if (run !== checkRun.current) return;
        setChecking(false);

        const found = fresh.find((form) => form.formSpecId === id);
        if (variant === "template") {
            if (found) {
                refuseId(
                    `A form with the ID "${id}" already exists. Choose another.`,
                );
            } else {
                void navigate(adminNewFormPath(id, title));
            }
            return;
        }
        // A typed ID that turns out to be taken gets the same warning as a
        // chosen form.
        const target = saveTargetFor(found?.versions);
        if (!found || target.kind === "new-form") {
            if (pickingExisting) {
                refuseChoice(
                    "That form is no longer in the forms list. Choose another.",
                );
            } else {
                void navigate(adminFormFromJsonPath(id, title));
            }
            return;
        }
        // The warning returns focus to whatever had it when it opened.
        submitRef.current?.focus();
        setWarning({ formSpecId: id, formName: displayName(found), target });
    }

    const text = PANEL_TEXT[variant];
    return (
        <form
            className={styles.newForm}
            aria-labelledby={headingId}
            onSubmit={(event) => void submit(event)}
        >
            <h2 id={headingId} className={styles.newFormHeading}>
                {text.heading}
            </h2>
            {variant === "json" && (
                <RadioGroup
                    label="What are you creating?"
                    value={kind}
                    onChange={(next) => {
                        changed();
                        setKind(next === "new" ? "new" : "existing");
                    }}
                >
                    <Radio value="existing" autoFocus={kind === "existing"}>
                        A new version of an existing form
                    </Radio>
                    <Radio value="new" autoFocus={kind === "new"}>
                        A new form
                    </Radio>
                </RadioGroup>
            )}
            {pickingExisting ? (
                <>
                    <div ref={selectRef}>
                        <Select
                            label="Form"
                            placeholder="Choose a form"
                            items={forms.map((form) => ({
                                id: form.formSpecId,
                                label: displayName(form),
                                description: form.title?.trim()
                                    ? form.formSpecId
                                    : undefined,
                            }))}
                            value={chosen}
                            onChange={(key) => {
                                changed();
                                setChosen(key === null ? null : String(key));
                            }}
                            isInvalid={choiceError !== null}
                            errorMessage={choiceError ?? undefined}
                        />
                    </div>
                    <label className={styles.newFormField}>
                        <span className={styles.newFormLabel}>Title</span>
                        <input
                            type="text"
                            value={chosenForm?.title?.trim() ?? ""}
                            readOnly
                            aria-describedby={titleHintId}
                        />
                    </label>
                    <p id={titleHintId} className={styles.hint}>
                        A new version keeps this form's title.
                    </p>
                </>
            ) : (
                <>
                    <label className={styles.newFormField}>
                        <span className={styles.newFormLabel}>Form ID</span>
                        <input
                            ref={idInputRef}
                            type="text"
                            value={formSpecId}
                            spellCheck={false}
                            aria-invalid={idError !== null}
                            aria-describedby={
                                [
                                    variant === "json" ? idHintId : "",
                                    idError ? idErrorId : "",
                                ]
                                    .filter(Boolean)
                                    .join(" ") || undefined
                            }
                            onChange={(e) => {
                                changed();
                                setFormSpecId(e.target.value);
                            }}
                        />
                    </label>
                    {variant === "json" && (
                        <p id={idHintId} className={styles.hint}>
                            Lowercase letters, numbers and hyphens.
                        </p>
                    )}
                    {idError && (
                        <p id={idErrorId} role="alert" className={styles.error}>
                            {idError}
                        </p>
                    )}
                    <label className={styles.newFormField}>
                        <span className={styles.newFormLabel}>Title</span>
                        <input
                            type="text"
                            value={title}
                            onChange={(e) => {
                                changed();
                                setTitle(e.target.value);
                            }}
                        />
                    </label>
                </>
            )}
            {checkFailed && (
                <p role="alert" className={styles.error}>
                    Couldn't check the form. Try again.
                </p>
            )}
            <div className={styles.newFormActions}>
                <button
                    ref={submitRef}
                    type="submit"
                    className={styles.newFormButton}
                >
                    {checking ? "Checking…" : text.submit}
                </button>
                <button
                    type="button"
                    className={styles.secondaryButton}
                    onClick={onCancel}
                >
                    Cancel
                </button>
            </div>
            {warning && (
                <ExistingFormWarning
                    choice={warning}
                    onConfirm={() => {
                        if (leaving.current) return;
                        leaving.current = true;
                        void navigate(
                            adminFormFromJsonPath(
                                warning.formSpecId,
                                "",
                                confirmValue(warning.target),
                            ),
                        );
                    }}
                    onCancel={() => setWarning(null)}
                />
            )}
        </form>
    );
}

// Asks before pasted JSON becomes a new version of an existing form, or
// replaces its unpublished draft; the same words as the JSON page uses.
function ExistingFormWarning({
    choice,
    onConfirm,
    onCancel,
}: Readonly<{
    choice: ExistingFormChoice;
    onConfirm: () => void;
    onCancel: () => void;
}>) {
    const bodyId = useId();
    const { heading, body, confirmLabel } = describeSaveTarget(
        choice.target,
        choice.formName,
        choice.formSpecId,
    );
    return (
        <Modal
            isOpen
            onOpenChange={(isOpen) => {
                if (!isOpen) onCancel();
            }}
        >
            <AlertDialog
                variant="warning"
                role="alertdialog"
                title={heading}
                aria-label={heading}
                aria-describedby={bodyId}
                isCloseable={false}
                buttons={
                    <>
                        <Button onPress={onConfirm}>{confirmLabel}</Button>
                        <Button variant="secondary" onPress={onCancel}>
                            Cancel
                        </Button>
                    </>
                }
            >
                <p id={bodyId}>{body}</p>
            </AlertDialog>
        </Modal>
    );
}

function displayName(form: FormSummary): string {
    return form.title?.trim() ? form.title : form.formSpecId;
}

function versionLabel(version: FormVersionSummary): string {
    return `v${version.version} ${version.isPublished ? "Published" : "Draft"}`;
}

// Each version label opens that version's JSON. The name is set in full, so
// it names the form too; a visually hidden suffix gains a stray space in Chrome.
function FormRow({
    form,
    onOpenVersion,
}: Readonly<{
    form: FormSummary;
    onOpenVersion: (version: FormVersionSummary) => void;
}>) {
    return (
        <li className={styles.item}>
            <Link
                to={adminFormEditorPath(form.formSpecId)}
                className={styles.formLink}
            >
                {displayName(form)}
            </Link>
            <p className={styles.formId}>{form.formSpecId}</p>
            <ul className={styles.versions}>
                {form.versions.map((version) => (
                    <li key={version.version}>
                        <button
                            type="button"
                            className={styles.versionButton}
                            aria-label={`${versionLabel(version)}, view JSON of ${displayName(form)}`}
                            onClick={() => onOpenVersion(version)}
                        >
                            <span className={styles.versionNumber}>
                                v{version.version}
                            </span>
                            <span className={styles.versionState}>
                                {version.isPublished ? "Published" : "Draft"}
                            </span>
                        </button>
                    </li>
                ))}
            </ul>
        </li>
    );
}
