import { useEffect, useRef, useState } from "react";
import { Button } from "@bcgov/design-system-react-components";

import styles from "./DocumentUpload.module.css";
import {
  ATTACHMENT_ACCEPT,
  type AttachmentPayload,
  AttachmentUploadError,
  attachmentErrorMessage,
  checkAttachmentFile,
  formatFileSize,
} from "@/api/attachments";
import { useUploadAttachment } from "@/hooks/useAttachments";
import {
  type AccountDocument,
  type DocumentField,
  MAX_DOCUMENTS,
} from "@/lib/accountContext";

const GENERAL_REFUSAL = "The document could not be attached. Try again later.";

interface DocumentUploadProps {
  /** The request field. The button carries it as its id, so a summary link can focus it. */
  field: DocumentField;
  /** Keep the file in the tab until sign-in, or upload it at once. */
  mode: "keep" | "upload";
  documents: AccountDocument[];
  /** Keep mode: whether this tab can keep files while the citizen signs in. */
  canKeep?: boolean;
  /** Keep mode: keeps an accepted file and lists it; rejects when the tab cannot keep it. */
  onKeep?: (file: File) => Promise<void>;
  /** Upload mode: lists a file the API has stored. */
  onUploaded?: (attachment: AttachmentPayload) => void;
  onRemove: (document: AccountDocument) => void;
  /** The field's error from the page's checks or the API. */
  error?: string;
}

interface Status {
  text: string;
  refused: boolean;
}

function refusalMessage(reason: unknown): string {
  if (reason instanceof AttachmentUploadError) {
    return attachmentErrorMessage(reason.keyword) ?? GENERAL_REFUSAL;
  }
  return GENERAL_REFUSAL;
}

/**
 * Attach documents for one purpose. Each file gets the API's own checks
 * first; then, before sign-in, it is kept in the tab, and after sign-in it is
 * uploaded. The design system has no upload control, so this is a hidden file
 * input behind a design system button.
 */
export default function DocumentUpload({
  field,
  mode,
  documents,
  canKeep = true,
  onKeep,
  onUploaded,
  onRemove,
  error,
}: DocumentUploadProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const upload = useUploadAttachment();
  const [busy, setBusy] = useState(false);
  // Set by Remove: its row goes, so focus returns to Attach document.
  const refocus = useRef(false);

  useEffect(() => {
    if (!refocus.current) return;
    refocus.current = false;
    document.getElementById(field)?.focus();
  }, [documents, field]);
  const [status, setStatus] = useState<Status | null>(null);

  if (mode === "keep" && !canKeep) {
    return (
      <p id={field} tabIndex={-1} className={styles.hint}>
        This browser cannot keep files while you sign in, so you will attach the
        document after signing in.
      </p>
    );
  }

  async function store(file: File) {
    if (mode === "keep") {
      await onKeep?.(file);
      return `${file.name} is ready. It is uploaded after you sign in.`;
    }
    const stored = await upload.mutateAsync(file);
    onUploaded?.(stored);
    return `${file.name} attached.`;
  }

  function refuse(file: File, message: string) {
    setStatus({
      text: `${file.name} was not attached. ${message}`,
      refused: true,
    });
  }

  async function take(file: File) {
    setBusy(true);
    setStatus({
      text: `${mode === "keep" ? "Checking" : "Uploading"} ${file.name}…`,
      refused: false,
    });
    try {
      const keyword = await checkAttachmentFile(file);
      if (keyword) {
        refuse(file, attachmentErrorMessage(keyword) ?? GENERAL_REFUSAL);
        return;
      }
      setStatus({ text: await store(file), refused: false });
    } catch (reason) {
      refuse(file, refusalMessage(reason));
    } finally {
      setBusy(false);
    }
  }

  function onFileChosen(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    // Cleared, so choosing the same file again still fires a change.
    event.target.value = "";
    if (file) void take(file);
  }

  function remove(doc: AccountDocument) {
    refocus.current = true;
    onRemove(doc);
    setStatus({
      text:
        doc.state === "attached"
          ? `${doc.fileName} removed. The stored copy is not deleted.`
          : `${doc.fileName} removed.`,
      refused: false,
    });
  }

  const full = documents.length >= MAX_DOCUMENTS;
  const hintId = `${field}-hint`;
  const errorId = `${field}-error`;
  const statusId = `${field}-status`;
  const describedBy = [
    hintId,
    error ? errorId : null,
    status?.refused ? statusId : null,
  ]
    .filter(Boolean)
    .join(" ");

  let hint = "PDF, PNG or JPEG, up to 5 MB each. Up to 5 documents.";
  if (full) {
    hint = `${MAX_DOCUMENTS} of ${MAX_DOCUMENTS} documents attached. Remove one to attach another.`;
  } else if (mode === "keep") {
    hint += " They are uploaded after you sign in.";
  }

  return (
    <div className={styles.upload}>
      <input
        ref={inputRef}
        type="file"
        accept={ATTACHMENT_ACCEPT}
        className={styles.fileInput}
        tabIndex={-1}
        aria-hidden="true"
        onChange={onFileChosen}
      />
      <div>
        <Button
          id={field}
          variant="secondary"
          isPending={busy}
          isDisabled={full}
          aria-describedby={describedBy}
          onPress={() => inputRef.current?.click()}
        >
          Attach document
        </Button>
      </div>
      <p id={hintId} className={styles.hint}>
        {hint}
      </p>
      {error && (
        <p id={errorId} className={styles.error}>
          {error}
        </p>
      )}
      <p
        id={statusId}
        aria-live="polite"
        className={status?.refused ? styles.error : styles.status}
      >
        {status?.text ?? ""}
      </p>
      {documents.length > 0 && (
        <ul className={styles.files} aria-label="Attached documents">
          {documents.map((doc) => (
            <li key={doc.id} className={styles.file}>
              <span className={styles.fileName}>{doc.fileName}</span>
              <span>{formatFileSize(doc.sizeBytes)}</span>
              <span
                className={
                  doc.state === "attached" ? styles.attached : styles.kept
                }
              >
                {doc.state === "attached"
                  ? "Attached"
                  : "Uploads after sign-in"}
              </span>
              <Button
                variant="tertiary"
                size="small"
                aria-label={`Remove ${doc.fileName}`}
                onPress={() => remove(doc)}
              >
                Remove
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
