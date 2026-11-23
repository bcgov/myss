import { useRef } from "react";
import { Button } from "@bcgov/design-system-react-components";

import styles from "./AttachmentUploadWidget.module.css";
import {
  ATTACHMENT_ACCEPT,
  AttachmentUploadError,
  attachmentErrorMessage,
} from "@/api/attachments";
import { useAttachments, useUploadAttachment } from "@/hooks/useAttachments";

function errorMessage(error: Error): string {
  if (error instanceof AttachmentUploadError) {
    return attachmentErrorMessage(error.keyword) ?? error.message;
  }
  return error.message;
}

function formatSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`;
  return `${(sizeBytes / 1024).toFixed(1)} KB`;
}

/**
 * Upload button plus the user's stored files. The button opens the native
 * file picker; the chosen file goes straight to the attachments API, and the
 * list refreshes once it's scanned and stored.
 */
export default function AttachmentUploadWidget() {
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
        accept={ATTACHMENT_ACCEPT}
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
      {attachments?.length === 0 && <p>None yet.</p>}
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
