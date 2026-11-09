import { Link, useParams } from "react-router";

import {
    FormMessageLoadError,
    type FormMessageSummary,
} from "@/api/formMessages";
import { useFormMessages } from "@/hooks/useFormMessages";
import { useFormName } from "@/hooks/useForms";
import { formatBcDateTime } from "@/lib/bcTime";
import { formMessagePath, paths } from "@/routes/paths";
import pageStyles from "./Page.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormMessagesPage.module.css";

function loadFailure(error: Error): string {
    if (error instanceof FormMessageLoadError && error.status === 403) {
        return "Your account is not allowed to open the form messages (error 403).";
    }
    const status =
        error instanceof FormMessageLoadError ? ` (error ${error.status})` : "";
    return `Could not load the form messages${status}. Reload the page to try again.`;
}

function MessagesTable({
    formSpecId,
    messages,
}: Readonly<{ formSpecId: string; messages: FormMessageSummary[] }>) {
    return (
        <div className={styles.tableWrap}>
            <table className={styles.table}>
                <thead>
                    <tr>
                        <th scope="col">Message</th>
                        <th scope="col">Where applicants see it</th>
                        <th scope="col">Last changed (BC time)</th>
                    </tr>
                </thead>
                <tbody>
                    {messages.map(
                        ({ definition, lastChangedAt, lastChangedBy }) => (
                            <tr key={definition.messageKey}>
                                <td>
                                    <Link
                                        to={formMessagePath(
                                            formSpecId,
                                            definition.messageKey,
                                        )}
                                        className={styles.messageLink}
                                    >
                                        {definition.name}
                                    </Link>
                                </td>
                                <td>{definition.description}</td>
                                <td>
                                    {lastChangedAt ? (
                                        `${formatBcDateTime(lastChangedAt)} by ${lastChangedBy}`
                                    ) : (
                                        <span className={styles.muted}>
                                            Not changed yet: applicants see the
                                            built-in message
                                        </span>
                                    )}
                                </td>
                            </tr>
                        ),
                    )}
                </tbody>
            </table>
        </div>
    );
}

// One form's messages, in the order of their definitions, each opening its
// editor. Reached from the form's row on Form management.
export default function FormMessagesPage() {
    const { formSpecId = "" } = useParams<{ formSpecId: string }>();
    const formName = useFormName(formSpecId);
    const { data: summaries, isPending, error } = useFormMessages();
    const messages = summaries?.filter(
        ({ definition }) => definition.formSpecId === formSpecId,
    );

    return (
        <div className={pageStyles.page}>
            <nav aria-label="Breadcrumb">
                <Link to={paths.formManagement}>← Form management</Link>
            </nav>

            <header className={adminStyles.heading}>
                <p className={adminStyles.eyebrow}>{formName}</p>
                <h1>Form messages</h1>
                <p className={styles.intro}>
                    Texts that applicants see on this form's pages. Saving a
                    message publishes it at once. A developer adds a message
                    here when a page starts showing it.
                </p>
            </header>

            {isPending && <p className={styles.status}>Loading form messages…</p>}

            {!messages && error && (
                <p role="alert" className={styles.error}>
                    {loadFailure(error)}
                </p>
            )}

            {messages?.length === 0 && (
                <p className={styles.status}>
                    This form has no messages to edit.{" "}
                    <Link to={paths.formManagement}>
                        Back to Form management
                    </Link>
                </p>
            )}

            {messages && messages.length > 0 && (
                <MessagesTable formSpecId={formSpecId} messages={messages} />
            )}
        </div>
    );
}
