import { Button, TextArea } from "@bcgov/design-system-react-components";

import styles from "./ReviewNotes.module.css";

import { ApplicationRequestError } from "@/api/intake";
import type { ReviewApplicationPayload } from "@/hooks/useReview";

// The internal notes on a file, oldest first, and — for the worker who has it
// Under Review — a box to add one. Notes are for workers only; the applicant
// never sees them. The draft text and the write belong to the review page,
// which must know about an unsaved note before it lets a decision through.

/** Mirrors NotePayload.MaxLength in MyssApi. */
const MAX_LENGTH = 4000;

function formatDate(value: string): string {
  return new Date(value).toLocaleString();
}

export default function ReviewNotes({
  application,
  text,
  onTextChange,
  onAdd,
  isBusy,
  error,
}: Readonly<{
  application: ReviewApplicationPayload;
  /** The note being written, not yet added. */
  text: string;
  onTextChange: (text: string) => void;
  onAdd: () => void;
  /** True while a note or an action is being saved. */
  isBusy: boolean;
  /** Why the last attempt to add a note failed, if it did. */
  error: Error | null;
}>) {
  return (
    <section className={styles.host} aria-labelledby="review-notes-heading">
      <h2 id="review-notes-heading" className={styles.heading}>
        Internal notes
      </h2>
      {application.notes.length === 0 ? (
        <p>No notes yet.</p>
      ) : (
        <ol className={styles.list}>
          {application.notes.map((note, index) => (
            <li key={`${note.createdAt}-${index}`} className={styles.note}>
              <p className={styles.meta}>
                {note.author}, {formatDate(note.createdAt)}
              </p>
              <p className={styles.text}>{note.text}</p>
            </li>
          ))}
        </ol>
      )}
      {application.canAddNote && (
        <div className={styles.add}>
          <TextArea
            label="Add a note"
            description="Only workers can see notes."
            value={text}
            onChange={onTextChange}
            maxLength={MAX_LENGTH}
          />
          {error && (
            <p role="alert" className={styles.notice}>
              {error instanceof ApplicationRequestError && error.isConflict
                ? "The note was not saved because the application changed. It has been reloaded; check it and add the note again."
                : `The note could not be saved: ${error.message}`}
            </p>
          )}
          <div>
            <Button
              variant="secondary"
              isDisabled={isBusy || text.trim() === ""}
              onPress={onAdd}
            >
              Add note
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}
