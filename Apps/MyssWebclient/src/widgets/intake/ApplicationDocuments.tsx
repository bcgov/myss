import { useEffect, useRef, useState } from "react";
import { Button } from "@bcgov/design-system-react-components";

import styles from "./ApplicationDocuments.module.css";
import {
  ATTACHMENT_ACCEPT,
  AttachmentUploadError,
  attachmentErrorMessage,
  checkAttachmentFile,
  formatFileSize,
} from "@/api/attachments";
import {
  useApplicationAttachments,
  useDeleteApplicationAttachment,
  useUploadApplicationAttachment,
} from "@/hooks/useAttachments";
import { INTAKE_KEYWORDS } from "@/hooks/useIntake";

// Supporting documents on an application. While the application is a draft
// or returned for more information, the applicant adds files with the Upload
// document button (the device's file picker) or by dropping them on the
// area, and can remove them; each file is checked here first, then the API
// checks it again and scans it. Once submitted the list is read-only, and the
// same files show on the worker's review screen.
//
// The design system has no upload control, so this is a hidden file input
// behind a design system button, inside a drop area. The button is the
// keyboard and screen reader path; dropping is an extra for pointer users.

const GENERAL_REFUSAL = "The document could not be uploaded. Try again later.";
const NOT_EDITABLE =
  "This application can no longer be changed, so documents cannot be added or removed. Reload to see its status.";

interface Status {
  text: string;
  refused: boolean;
}

function refusalMessage(reason: unknown): string {
  if (reason instanceof AttachmentUploadError) {
    if (reason.keyword === INTAKE_KEYWORDS.notEditable) return NOT_EDITABLE;
    return attachmentErrorMessage(reason.keyword) ?? GENERAL_REFUSAL;
  }
  return GENERAL_REFUSAL;
}

export default function ApplicationDocuments({
  applicationId,
  editable,
}: Readonly<{ applicationId: string; editable: boolean }>) {
  const {
    data: documents,
    error,
    isPending,
  } = useApplicationAttachments(applicationId);
  const upload = useUploadApplicationAttachment(applicationId);
  const remove = useDeleteApplicationAttachment(applicationId);
  const inputRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [status, setStatus] = useState<Status | null>(null);

  const headingId = `documents-${applicationId}-heading`;
  const hintId = `documents-${applicationId}-hint`;
  const statusId = `documents-${applicationId}-status`;
  const buttonId = `documents-${applicationId}-upload`;

  // A file dropped just outside the drop area would otherwise make the
  // browser open it, leaving the page and its unsaved answers. While files
  // can be added, the page ignores drops; the area handles its own first.
  useEffect(() => {
    if (!editable) return;
    function ignoreFileDrop(event: DragEvent) {
      if (event.dataTransfer?.types.includes("Files")) event.preventDefault();
    }
    window.addEventListener("dragover", ignoreFileDrop);
    window.addEventListener("drop", ignoreFileDrop);
    return () => {
      window.removeEventListener("dragover", ignoreFileDrop);
      window.removeEventListener("drop", ignoreFileDrop);
    };
  }, [editable]);

  async function take(files: File[]) {
    if (files.length === 0) return;
    setBusy(true);
    const done: string[] = [];
    const refused: string[] = [];
    try {
      for (const file of files) {
        setStatus({ text: `Uploading ${file.name}…`, refused: false });
        try {
          const keyword = await checkAttachmentFile(file);
          if (keyword) {
            refused.push(
              `${file.name} was not uploaded. ${attachmentErrorMessage(keyword) ?? GENERAL_REFUSAL}`,
            );
            continue;
          }
          await upload.mutateAsync(file);
          done.push(`${file.name} uploaded.`);
        } catch (reason) {
          refused.push(
            `${file.name} was not uploaded. ${refusalMessage(reason)}`,
          );
          // Submitted or withdrawn elsewhere: the rest would be refused too.
          if (
            reason instanceof AttachmentUploadError &&
            reason.keyword === INTAKE_KEYWORDS.notEditable
          ) {
            break;
          }
        }
      }
    } finally {
      setBusy(false);
      setStatus({
        text: [...done, ...refused].join(" "),
        refused: refused.length > 0,
      });
    }
  }

  function onFilesChosen(event: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    // Cleared, so choosing the same file again still fires a change.
    event.target.value = "";
    void take(files);
  }

  function onDragOver(event: React.DragEvent<HTMLDivElement>) {
    if (!editable || busy) return;
    if (!Array.from(event.dataTransfer.types).includes("Files")) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = "copy";
    setDragging(true);
  }

  function onDragLeave(event: React.DragEvent<HTMLDivElement>) {
    // Leaving for a child of the area is not leaving the area.
    if (event.currentTarget.contains(event.relatedTarget as Node | null))
      return;
    setDragging(false);
  }

  function onDrop(event: React.DragEvent<HTMLDivElement>) {
    event.preventDefault();
    setDragging(false);
    if (!editable || busy) return;
    void take(Array.from(event.dataTransfer.files));
  }

  async function removeDocument(id: string, fileName: string) {
    try {
      await remove.mutateAsync(id);
      setStatus({ text: `${fileName} removed.`, refused: false });
    } catch (reason) {
      setStatus({
        text: `${fileName} was not removed. ${refusalMessage(reason)}`,
        refused: true,
      });
    }
    // Its row is gone, so focus goes back to the upload button.
    document.getElementById(buttonId)?.focus();
  }

  return (
    <section className={styles.documents} aria-labelledby={headingId}>
      <h2 id={headingId} className={styles.heading}>
        Supporting documents
      </h2>
      {editable ? (
        <p id={hintId} className={styles.hint}>
          Add any documents that support your application, such as pay stubs,
          bank statements or ID. PDF, PNG or JPEG, up to 5 MB each. They are
          sent with your application when you submit it.
        </p>
      ) : (
        <p className={styles.hint}>The documents sent with this application.</p>
      )}

      {editable && (
        <div
          className={
            dragging ? `${styles.dropArea} ${styles.dragging}` : styles.dropArea
          }
          onDragOver={onDragOver}
          onDragEnter={onDragOver}
          onDragLeave={onDragLeave}
          onDrop={onDrop}
          data-testid="document-drop-area"
        >
          <input
            ref={inputRef}
            type="file"
            multiple
            accept={ATTACHMENT_ACCEPT}
            className={styles.fileInput}
            tabIndex={-1}
            aria-hidden="true"
            onChange={onFilesChosen}
          />
          <p className={styles.dropText} aria-hidden="true">
            Drag and drop files here, or
          </p>
          <Button
            id={buttonId}
            variant="secondary"
            isPending={busy}
            aria-describedby={
              status?.refused ? `${hintId} ${statusId}` : hintId
            }
            onPress={() => inputRef.current?.click()}
          >
            Upload document
          </Button>
        </div>
      )}

      <p
        id={statusId}
        aria-live="polite"
        className={status?.refused ? styles.error : styles.status}
      >
        {status?.text ?? ""}
      </p>

      {isPending && <p className={styles.hint}>Loading documents…</p>}
      {error && (
        <p role="alert" className={styles.error}>
          The documents could not be loaded. Reload the page to try again.
        </p>
      )}
      {documents && documents.length === 0 && !editable && (
        <p className={styles.hint}>
          No documents were sent with this application.
        </p>
      )}
      {documents && documents.length > 0 && (
        <ul className={styles.files} aria-label="Uploaded documents">
          {documents.map((doc) => (
            <li key={doc.id} className={styles.file}>
              <span className={styles.fileName}>{doc.fileName}</span>
              <span className={styles.meta}>
                {formatFileSize(doc.sizeBytes)}
              </span>
              {editable && (
                <Button
                  variant="tertiary"
                  size="small"
                  isDisabled={remove.isPending}
                  aria-label={`Remove ${doc.fileName}`}
                  onPress={() => void removeDocument(doc.id, doc.fileName)}
                >
                  Remove
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
