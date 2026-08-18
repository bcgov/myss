import { useRef } from "react";
import { Button } from "@bcgov/design-system-react-components";

import styles from "./AttachmentUpload.module.css";
import { AttachmentUploadError } from "@/api/attachments";
import { useAttachments, useUploadAttachment } from "@/hooks/useAttachments";

// Mirrors the backend allow-list (Attachments:AllowedContentTypes). The
// native picker filter is a courtesy — the API re-checks type, magic bytes
// and size regardless.
const ACCEPT = "application/pdf,image/png,image/jpeg";

// The API's stable keywords, mapped to display text. Keywords double as
// content keys, so these strings move to Strapi once the string pipeline
// exists (forms architecture §2.6).
const KEYWORD_MESSAGES: Record<string, string> = {
  "DOC.UPLOAD.EMPTY": "The selected file is empty.",
  "DOC.UPLOAD.TOO_LARGE": "The file is too large. The limit is 5 MB.",
  "DOC.UPLOAD.TYPE_NOT_ALLOWED": "Only PDF, PNG and JPEG files are accepted.",
  "DOC.UPLOAD.INFECTED":
    "The file failed the virus scan and was not stored.",
  "DOC.SCAN.UNAVAILABLE":
    "The virus scanner is unavailable right now. Try again in a few minutes.",
};

function errorMessage(error: Error): string {
  if (error instanceof AttachmentUploadError && error.keyword) {
    return KEYWORD_MESSAGES[error.keyword] ?? error.message;
  }
  return error.message;
}

function formatSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`;
  return `${(sizeBytes / 1024).toFixed(1)} KB`;
}

/**
 * The upload tech demo: a button that opens the native file browser and
 * submits the chosen file to the attachments API (validate -> ClamAV scan ->
 * object storage), plus the caller's stored files.
 */
export default function AttachmentUpload() {
  const inputRef = useRef<HTMLInputElement>(null);
  const { data: attachments } = useAttachments();
  const upload = useUploadAttachment();

  function onFileChosen(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) upload.mutate(file);
    // Clear the value so choosing the same file again still fires onChange.
    event.target.value = "";
  }

  return (
    <section>
      <input
        ref={inputRef}
        type="file"
        accept={ACCEPT}
        aria-label="Choose a file to upload"
        className={styles.fileInput}
        onChange={onFileChosen}
      />
      <Button
        variant="primary"
        isDisabled={upload.isPending}
        onPress={() => inputRef.current?.click()}
      >
        {upload.isPending ? "Uploading…" : "Upload a file"}
      </Button>

      <div aria-live="polite">
        {upload.error && (
          <p className={styles.error}>{errorMessage(upload.error)}</p>
        )}
        {upload.data && (
          <p>
            Uploaded <strong>{upload.data.fileName}</strong> — scanned and
            stored.
          </p>
        )}
      </div>

      <h3>Your files</h3>
      {attachments && attachments.length === 0 && <p>None yet.</p>}
      {attachments && attachments.length > 0 && (
        <ul>
          {attachments.map((a) => (
            <li key={a.id}>
              {a.fileName} — {formatSize(a.sizeBytes)} —{" "}
              {new Date(a.uploadedAt).toLocaleString()}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
