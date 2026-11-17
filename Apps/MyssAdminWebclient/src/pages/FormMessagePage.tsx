import { Fragment, useEffect, useRef, useState } from "react";
import {
    Accordion,
    Button,
    InlineAlert,
    TextArea,
    TextField,
} from "@bcgov/design-system-react-components";
import {
    builtInFormMessage,
    FORM_MESSAGE_KEYWORDS,
    FORM_MESSAGE_LIMITS,
    validateFormMessage,
    type FormMessageField,
    type FormMessagePlaceholder,
    type FormMessageTexts,
} from "@myss/web-common/api";
import { MessageText } from "@myss/web-common/components";
import { Link, useParams } from "react-router";

import {
    FormMessageLoadError,
    FormMessageRejectedError,
    type FormMessageDetail,
    type FormMessageEntry,
} from "@/api/formMessages";
import ErrorSummary from "@/components/ErrorSummary";
import { useFormMessage, useSaveFormMessage } from "@/hooks/useFormMessages";
import { useFormName } from "@/hooks/useForms";
import { formatBcDateTime } from "@/lib/bcTime";
import { formMessagesPath } from "@/routes/paths";
import pageStyles from "./Page.module.css";
import adminStyles from "./AdminPage.module.css";
import styles from "./FormMessagePage.module.css";

// One form message: what applicants see, who changed it when, and an editor
// for its three texts with a live preview. Save checks the rules first and
// publishes at once.

/** A reason a save was refused, on the field it concerns when there is one. */
interface Refusal {
    field?: FormMessageField;
    message: string;
}

/** An edit in progress, and the live entry it started from. */
interface Edit {
    basedOn: string | null;
    texts: FormMessageTexts;
}

type FocusTarget = "firstField" | "editButton";

const FIELDS: readonly FormMessageField[] = ["heading", "title", "body"];

const CHANGED_SINCE_EDIT =
    "This message changed after you started editing. Your changes are still here. Cancel to see the current message, then make your changes again.";

const UNCONFIRMED =
    "The message may not have been saved. Your changes are still here: try again, or cancel and check the message.";

function isField(value: string): value is FormMessageField {
    return (FIELDS as readonly string[]).includes(value);
}

function describeFailure(error: Error): Refusal[] {
    if (!(error instanceof FormMessageRejectedError)) {
        return [{ message: `The save could not be confirmed. ${UNCONFIRMED}` }];
    }
    if (error.errors.some((e) => e.keyword === FORM_MESSAGE_KEYWORDS.changed)) {
        return [{ message: CHANGED_SINCE_EDIT }];
    }
    if (error.errors.length > 0) {
        return error.errors.map(({ field, message }) =>
            isField(field) ? { field, message } : { message },
        );
    }
    if (error.status === 401) {
        return [{ message: "Could not save the message: you are no longer signed in (error 401). Sign in again, then make your changes again." }];
    }
    if (error.status === 403) {
        return [{ message: "Could not save the message: your account is not allowed to change it (error 403)." }];
    }
    if (error.status >= 500) {
        return [{ message: `The save could not be confirmed (error ${error.status}). ${UNCONFIRMED}` }];
    }
    return [{ message: `Could not save the message (error ${error.status}). Your changes are still here: try again, or cancel.` }];
}

function sampleValues(
    placeholders: readonly FormMessagePlaceholder[],
): Record<string, string> {
    return Object.fromEntries(placeholders.map((p) => [p.name, p.sample]));
}

/** The applicant's view of a message, with each placeholder's sample value. */
function Preview({
    texts,
    placeholders,
    caption,
}: Readonly<{
    texts: FormMessageTexts;
    placeholders: readonly FormMessagePlaceholder[];
    caption: string;
}>) {
    return (
        <figure className={styles.preview}>
            <figcaption className={styles.previewCaption}>
                <strong>{caption}</strong>
                {placeholders.map((p) => (
                    <span key={p.name}>
                        {`{${p.name}}`} shows the sample {p.sample}
                    </span>
                ))}
            </figcaption>
            <div className={styles.previewBody}>
                <p className={styles.previewHeading}>{texts.heading}</p>
                <InlineAlert variant="success">
                    {/* With children, the alert does not render its title
                        prop, so the title uses the alert's own markup. */}
                    <span className="title" id="alert-title">
                        {texts.title}
                    </span>
                    <div className={styles.previewMessage}>
                        <MessageText
                            text={texts.body}
                            values={sampleValues(placeholders)}
                        />
                    </div>
                </InlineAlert>
            </div>
        </figure>
    );
}

function capitalise(text: string): string {
    return text.charAt(0).toUpperCase() + text.slice(1);
}

const EXAMPLE =
    "We have your request. **Keep this page for your records.**\n\n" +
    "If you have questions:\n" +
    "- [Visit our website](https://www2.gov.bc.ca)\n" +
    "- [Email us](mailto:name@example.com)";

function FormattingHelp({
    placeholders,
}: Readonly<{ placeholders: readonly FormMessagePlaceholder[] }>) {
    return (
        <Accordion label="Formatting help">
            <div className={styles.help}>
                <p>
                    Type these marks in the message. The preview shows how
                    applicants will see it.
                </p>
                <dl>
                    <dt>**bold text**</dt>
                    <dd>Bold</dd>
                    <dt>[link text](https://…)</dt>
                    <dd>A link; also mailto: and tel:</dd>
                    <dt>- item</dt>
                    <dd>A bullet point, one per line</dd>
                    <dt>(blank line)</dt>
                    <dd>A new paragraph; a single line break starts a new line</dd>
                </dl>
                <p className={styles.helpLabel}>
                    Placeholders this message can use
                </p>
                {placeholders.length === 0 ? (
                    <p>None.</p>
                ) : (
                    <dl>
                        {placeholders.map((p) => (
                            <Fragment key={p.name}>
                                <dt>{`{${p.name}}`}</dt>
                                <dd>
                                    {capitalise(p.description)}{" "}
                                    {p.required ? "(required)" : "(optional)"}
                                </dd>
                            </Fragment>
                        ))}
                    </dl>
                )}
                <p className={styles.helpLabel}>Example</p>
                <pre>{EXAMPLE}</pre>
            </div>
        </Accordion>
    );
}

function LastChanged({ current }: Readonly<{ current: FormMessageEntry | null }>) {
    if (!current) {
        return (
            <p className={styles.lastChanged}>
                Not changed yet. Applicants see the built-in message.
            </p>
        );
    }
    return (
        <p className={styles.lastChanged}>
            Last changed on <strong>{formatBcDateTime(current.changedAt)}</strong>{" "}
            by <strong>{current.changedBy}</strong>
        </p>
    );
}

function HistoryRow({
    entry,
    isLive,
}: Readonly<{ entry: FormMessageEntry; isLive: boolean }>) {
    const [open, setOpen] = useState(false);
    const detailId = `history-${entry.id}`;
    return (
        <>
            <tr>
                <td>{formatBcDateTime(entry.changedAt)}</td>
                <td>
                    {entry.changedBy}{" "}
                    {isLive && <span className={styles.live}>Live</span>}
                </td>
                <td>
                    <button
                        type="button"
                        className={styles.textButton}
                        aria-label={`${open ? "Hide" : "Show"} text of the change on ${formatBcDateTime(entry.changedAt)}`}
                        aria-expanded={open}
                        aria-controls={detailId}
                        onClick={() => setOpen(!open)}
                    >
                        {open ? "Hide text" : "Show text"}
                    </button>
                </td>
            </tr>
            {open && (
                <tr id={detailId} className={styles.detail}>
                    <td colSpan={3}>
                        <dl>
                            <dt>Page heading</dt>
                            <dd>{entry.heading}</dd>
                            <dt>Message title</dt>
                            <dd>{entry.title}</dd>
                            <dt>Message</dt>
                            <dd>
                                <pre>{entry.body}</pre>
                            </dd>
                        </dl>
                    </td>
                </tr>
            )}
        </>
    );
}

function History({ entries }: Readonly<{ entries: FormMessageEntry[] }>) {
    return (
        <section aria-labelledby="history-heading" className={styles.history}>
            <h2 id="history-heading" className={styles.sectionHeading}>
                Change history
            </h2>
            {entries.length === 0 ? (
                <p>No changes yet.</p>
            ) : (
                <div className={styles.tableWrap}>
                    <table className={styles.table}>
                        <thead>
                            <tr>
                                <th scope="col">Date and time (BC)</th>
                                <th scope="col">Changed by</th>
                                <th scope="col">Text</th>
                            </tr>
                        </thead>
                        <tbody>
                            {entries.map((entry, index) => (
                                <HistoryRow
                                    key={entry.id}
                                    entry={entry}
                                    isLive={index === 0}
                                />
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </section>
    );
}

/** What the read view shows: the live message, else the built-in one, else nothing. */
function shownTexts(
    message: FormMessageDetail,
): FormMessageTexts | undefined {
    const { definition, current } = message;
    return (
        current ??
        builtInFormMessage(definition.formSpecId, definition.messageKey)
    );
}

function ReadView({
    message,
    onEdit,
}: Readonly<{ message: FormMessageDetail; onEdit: () => void }>) {
    const texts = shownTexts(message);
    return (
        <div className={styles.view}>
            <LastChanged current={message.current} />
            {texts ? (
                <Preview
                    texts={texts}
                    placeholders={message.definition.placeholders}
                    caption="What applicants see"
                />
            ) : (
                <p>This message has no built-in text, and none is saved yet.</p>
            )}
            <div className={styles.actions}>
                <Button variant="secondary" data-message-edit onPress={onEdit}>
                    Edit
                </Button>
            </div>
        </div>
    );
}

function EditView({
    edit,
    placeholders,
    refusals,
    isSaving,
    onChange,
    onSave,
    onCancel,
}: Readonly<{
    edit: Edit;
    placeholders: readonly FormMessagePlaceholder[];
    refusals: readonly Refusal[];
    isSaving: boolean;
    onChange: (field: FormMessageField, text: string) => void;
    onSave: () => void;
    onCancel: () => void;
}>) {
    function fieldProps(field: FormMessageField) {
        const messages = refusals
            .filter((refusal) => refusal.field === field)
            .map((refusal) => refusal.message);
        return {
            name: field,
            isRequired: true,
            value: edit.texts[field],
            onChange: (text: string) => onChange(field, text),
            isReadOnly: isSaving,
            isInvalid: messages.length > 0,
            errorMessage: messages.length > 0 ? messages.join(" ") : undefined,
        };
    }

    return (
        <div className={styles.editGrid}>
            <div className={styles.fields}>
                <TextField label="Page heading" {...fieldProps("heading")} />
                <TextField label="Message title" {...fieldProps("title")} />
                <TextArea
                    label="Message"
                    description={`At most ${FORM_MESSAGE_LIMITS.body.toLocaleString("en-CA")} characters, formatting marks included.`}
                    maxLength={FORM_MESSAGE_LIMITS.body}
                    {...fieldProps("body")}
                />
                <FormattingHelp placeholders={placeholders} />
                <div className={styles.actions}>
                    <Button variant="primary" isPending={isSaving} onPress={onSave}>
                        Save
                    </Button>
                    <Button variant="secondary" isDisabled={isSaving} onPress={onCancel}>
                        Cancel
                    </Button>
                </div>
            </div>
            <Preview
                texts={edit.texts}
                placeholders={placeholders}
                caption="Preview, updated as you type"
            />
        </div>
    );
}

function MessageEditor({
    formSpecId,
    messageKey,
    message,
}: Readonly<{
    formSpecId: string;
    messageKey: string;
    message: FormMessageDetail;
}>) {
    const save = useSaveFormMessage(formSpecId, messageKey);
    const [edit, setEdit] = useState<Edit | null>(null);
    const [refusals, setRefusals] = useState<Refusal[]>([]);
    const [refusalCount, setRefusalCount] = useState(0);
    const [statusMessage, setStatusMessage] = useState("");
    const editorRef = useRef<HTMLDivElement>(null);
    const pendingFocus = useRef<FocusTarget | null>(null);
    const { definition, current } = message;

    // Moves focus once the render that shows its target has happened.
    useEffect(() => {
        const target = pendingFocus.current;
        if (!target) return;
        pendingFocus.current = null;
        const selector =
            target === "firstField" ? "input" : "[data-message-edit]";
        editorRef.current?.querySelector<HTMLElement>(selector)?.focus();
    });

    function startEditing() {
        const from =
            current ?? builtInFormMessage(formSpecId, messageKey) ?? {
                heading: "",
                title: "",
                body: "",
            };
        setEdit({
            basedOn: current?.id ?? null,
            texts: { heading: from.heading, title: from.title, body: from.body },
        });
        setRefusals([]);
        setStatusMessage("");
        pendingFocus.current = "firstField";
    }

    function finishEditing(status: string) {
        setEdit(null);
        setRefusals([]);
        setStatusMessage(status);
        pendingFocus.current = "editButton";
    }

    function refuse(reasons: Refusal[]) {
        setRefusals(reasons);
        setRefusalCount((count) => count + 1);
    }

    // An edited field's refusals go at once; the others stay until the next Save or Cancel.
    function change(field: FormMessageField, text: string) {
        setEdit((now) => now && { ...now, texts: { ...now.texts, [field]: text } });
        setRefusals((now) => now.filter((refusal) => refusal.field !== field));
    }

    function saveEdit() {
        if (!edit) return;
        const invalid = validateFormMessage(edit.texts, definition.placeholders);
        if (invalid.length > 0) {
            refuse(invalid);
            return;
        }
        setRefusals([]);
        save.mutate(
            { basedOn: edit.basedOn, ...edit.texts },
            {
                onSuccess: () => finishEditing("Form message saved and published."),
                onError: (failure) => refuse(describeFailure(failure)),
            },
        );
    }

    function cancel() {
        save.reset();
        finishEditing("");
    }

    function focusField(field: FormMessageField) {
        editorRef.current
            ?.querySelector<HTMLElement>(`[name="${field}"]`)
            ?.focus();
    }

    return (
        <div ref={editorRef} className={styles.editor}>
            <p className={styles.intro}>
                Save publishes the message at once, and applicants see the
                change within a minute.
            </p>
            {/* Announced and focused on each refusal, so a screen-reader user
                is not left at the button. */}
            <ErrorSummary
                items={refusals.map(({ field, message: text }) => ({
                    message: text,
                    onSelect: field && edit ? () => focusField(field) : undefined,
                }))}
                focusKey={refusalCount > 0 ? refusalCount : null}
                announce
            />
            <p role="status" className={styles.saved}>
                {statusMessage}
            </p>
            {edit ? (
                <EditView
                    edit={edit}
                    placeholders={definition.placeholders}
                    refusals={refusals}
                    isSaving={save.isPending}
                    onChange={change}
                    onSave={saveEdit}
                    onCancel={cancel}
                />
            ) : (
                <ReadView message={message} onEdit={startEditing} />
            )}
            <History entries={message.history} />
        </div>
    );
}

function LoadStatus({
    isPending,
    error,
    formSpecId,
    messageKey,
}: Readonly<{
    isPending: boolean;
    error: Error | null;
    formSpecId: string;
    messageKey: string;
}>) {
    if (isPending || !error) {
        return <p className={styles.status}>Loading the message…</p>;
    }
    const status = error instanceof FormMessageLoadError ? error.status : null;
    if (status === 403) {
        return (
            <p role="alert" className={styles.error}>
                Your account is not allowed to open the form messages (error
                403).
            </p>
        );
    }
    if (status === 404) {
        return (
            <p role="alert" className={styles.error}>
                This form has no message called “{messageKey}”.{" "}
                <Link to={formMessagesPath(formSpecId)}>
                    Back to Form messages
                </Link>
            </p>
        );
    }
    const suffix = status === null ? "" : ` (error ${status})`;
    return (
        <p role="alert" className={styles.error}>
            Could not load the message{suffix}. Reload the page to try again.
        </p>
    );
}

export default function FormMessagePage() {
    const { formSpecId = "", messageKey = "" } = useParams<{
        formSpecId: string;
        messageKey: string;
    }>();
    const formName = useFormName(formSpecId);
    const { data: message, isPending, error } = useFormMessage(
        formSpecId,
        messageKey,
    );

    return (
        <div className={pageStyles.page}>
            <nav aria-label="Breadcrumb">
                <Link to={formMessagesPath(formSpecId)}>← Form messages</Link>
            </nav>

            <header className={adminStyles.heading}>
                <p className={adminStyles.eyebrow}>{formName}</p>
                <h1>{message?.definition.name ?? "Form message"}</h1>
                {message && (
                    <p className={styles.description}>
                        {message.definition.description}
                    </p>
                )}
            </header>

            {message ? (
                <MessageEditor
                    key={`${formSpecId}/${messageKey}`}
                    formSpecId={formSpecId}
                    messageKey={messageKey}
                    message={message}
                />
            ) : (
                <LoadStatus
                    isPending={isPending}
                    error={error}
                    formSpecId={formSpecId}
                    messageKey={messageKey}
                />
            )}
        </div>
    );
}
