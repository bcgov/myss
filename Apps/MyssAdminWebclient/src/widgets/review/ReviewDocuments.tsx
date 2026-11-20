import { Button } from "@bcgov/design-system-react-components";
import { useQuery } from "@tanstack/react-query";
import { useState } from "react";

import styles from "./ReviewDocuments.module.css";
import {
  downloadReviewAttachment,
  listReviewAttachments,
  type ReviewAttachmentPayload,
} from "@/api/review";

// The supporting documents the applicant sent with a submitted application.
// Each one downloads through the API (it needs the worker's token), then
// saves from an object URL under its original name.

function formatSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} bytes`;
  if (sizeBytes < 1024 * 1024) return `${Math.round(sizeBytes / 1024)} KB`;
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
}

function save(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  // After the click has handed the URL to the download.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

export default function ReviewDocuments({
  applicationId,
}: Readonly<{ applicationId: string }>) {
  const { data, error, isPending } = useQuery({
    queryKey: ["review", "applications", applicationId, "attachments"],
    queryFn: () => listReviewAttachments(applicationId),
  });
  const [downloading, setDownloading] = useState<string | null>(null);
  const [failed, setFailed] = useState<string | null>(null);

  async function download(doc: ReviewAttachmentPayload) {
    setDownloading(doc.id);
    setFailed(null);
    try {
      save(await downloadReviewAttachment(applicationId, doc.id), doc.fileName);
    } catch {
      setFailed(`${doc.fileName} could not be downloaded. Try again.`);
    } finally {
      setDownloading(null);
    }
  }

  return (
    <section aria-labelledby="review-documents-heading">
      <h2 id="review-documents-heading">Supporting documents</h2>
      {isPending && <p>Loading documents…</p>}
      {error && (
        <p role="alert">
          The documents could not be loaded. Reload to try again.
        </p>
      )}
      {data && data.length === 0 && (
        <p>The applicant did not send any documents.</p>
      )}
      {data && data.length > 0 && (
        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">Document</th>
              <th scope="col">Size</th>
              <th scope="col">Uploaded</th>
              <th scope="col">
                <span className={styles.visuallyHidden}>Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {data.map((doc) => (
              <tr key={doc.id}>
                <td>{doc.fileName}</td>
                <td>{formatSize(doc.sizeBytes)}</td>
                <td>{new Date(doc.uploadedAt).toLocaleString()}</td>
                <td>
                  <Button
                    variant="tertiary"
                    size="small"
                    isPending={downloading === doc.id}
                    aria-label={`Download ${doc.fileName}`}
                    onPress={() => void download(doc)}
                  >
                    Download
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {failed && <p role="alert">{failed}</p>}
    </section>
  );
}
