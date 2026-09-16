import { Link } from "react-router";

import { useAdminForms } from "@/hooks/useForms";
import { adminFormEditorPath } from "@/routes/paths";
import type { FormSummary } from "@/api/forms";
import homeStyles from "./HomePage.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormManagementPage.module.css";

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
        </div>
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
