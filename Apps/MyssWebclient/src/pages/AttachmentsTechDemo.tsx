import { Link } from "react-router";

import AttachmentUpload from "@/components/AttachmentUpload";

/**
 * Attachments tech demo: upload a file through the scanned attachment
 * pipeline (ClamAV -> S3-compatible storage).
 */
export default function AttachmentsTechDemo() {
  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to="/techdemos">← Tech demos</Link>
      </nav>
      <h1>Attachments tech demo</h1>
      <p>
        Files are virus-scanned before they are stored; only PDF, PNG and JPEG
        up to 5 MB are accepted.
      </p>
      <AttachmentUpload />
    </>
  );
}
