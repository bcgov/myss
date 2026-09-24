import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Link, useNavigate } from "react-router";

import { useAdminForms } from "@/hooks/useForms";
import { adminFormEditorPath, adminNewFormPath } from "@/routes/paths";
import type { FormSummary } from "@/api/forms";
import homeStyles from "./HomePage.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormManagementPage.module.css";

// Mirrors the pattern MyssContent's form-spec schema enforces on formSpecId,
// so a bad ID is an inline hint here instead of a refused write later.
const FORM_ID_PATTERN = /^[a-z0-9-]+$/;

// The admin forms list: every seeded form with its versions and which one is
// published. Each row links into the editor (the route is registered later).
export default function FormManagementPage() {
    const { data: forms, isPending, error } = useAdminForms();

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

            {!isPending && !error && forms && forms.length === 0 && (
                <p className={styles.status}>No forms found.</p>
            )}

            {!isPending && !error && forms && forms.length > 0 && (
                <ul className={styles.list}>
                    {forms.map((form) => (
                        <FormRow key={form.formSpecId} form={form} />
                    ))}
                </ul>
            )}

            {!isPending && !error && forms && (
                <NewFormSection forms={forms} />
            )}
        </div>
    );
}

// Collects the ID and title for a form that does not exist yet, then opens the
// editor on a local template; nothing is stored until the first Save draft.
function NewFormSection({ forms }: { forms: FormSummary[] }) {
    const navigate = useNavigate();
    const [open, setOpen] = useState(false);
    const [formSpecId, setFormSpecId] = useState("");
    const [title, setTitle] = useState("");
    const [error, setError] = useState<string | null>(null);

    // Opening and cancelling swap what is on screen under the user's focus;
    // move it along rather than letting it fall to the document body. The
    // guard keeps page load from stealing focus.
    const idInputRef = useRef<HTMLInputElement>(null);
    const openButtonRef = useRef<HTMLButtonElement>(null);
    const toggled = useRef(false);
    useEffect(() => {
        if (!toggled.current) return;
        if (open) {
            idInputRef.current?.focus();
        } else {
            openButtonRef.current?.focus();
        }
    }, [open]);

    function toggle(next: boolean) {
        toggled.current = true;
        setOpen(next);
    }

    function create(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const id = formSpecId.trim();
        if (!FORM_ID_PATTERN.test(id)) {
            setError(
                "Enter a form ID using only lowercase letters, numbers and hyphens.",
            );
            return;
        }
        if (forms.some((form) => form.formSpecId === id)) {
            setError(`A form with the ID "${id}" already exists.`);
            return;
        }
        navigate(adminNewFormPath(id, title));
    }

    if (!open) {
        return (
            <button
                ref={openButtonRef}
                type="button"
                className={styles.newFormButton}
                onClick={() => toggle(true)}
            >
                New form
            </button>
        );
    }

    return (
        <form
            className={styles.newForm}
            aria-label="New form"
            onSubmit={create}
        >
            <label className={styles.newFormField}>
                <span className={styles.newFormLabel}>Form ID</span>
                <input
                    ref={idInputRef}
                    type="text"
                    value={formSpecId}
                    aria-invalid={error !== null}
                    aria-describedby={error ? "new-form-error" : undefined}
                    onChange={(e) => {
                        setFormSpecId(e.target.value);
                        setError(null);
                    }}
                />
            </label>
            <label className={styles.newFormField}>
                <span className={styles.newFormLabel}>Title</span>
                <input
                    type="text"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                />
            </label>
            {error && (
                <p id="new-form-error" role="alert" className={styles.error}>
                    {error}
                </p>
            )}
            <div className={styles.newFormActions}>
                <button type="submit" className={styles.newFormButton}>
                    Create
                </button>
                <button
                    type="button"
                    className={styles.newFormCancel}
                    onClick={() => toggle(false)}
                >
                    Cancel
                </button>
            </div>
        </form>
    );
}

function FormRow({ form }: { form: FormSummary }) {
    return (
        <li className={styles.item}>
            <Link
                to={adminFormEditorPath(form.formSpecId)}
                className={styles.formLink}
            >
                {form.title?.trim() ? form.title : form.formSpecId}
            </Link>
            <p className={styles.formId}>{form.formSpecId}</p>
            <ul className={styles.versions}>
                {form.versions.map((version) => (
                    <li key={version.version} className={styles.versionItem}>
                        <span className={styles.versionNumber}>
                            v{version.version}
                        </span>
                        <span className={styles.versionState}>
                            {version.isPublished ? "Published" : "Draft"}
                        </span>
                    </li>
                ))}
            </ul>
        </li>
    );
}
