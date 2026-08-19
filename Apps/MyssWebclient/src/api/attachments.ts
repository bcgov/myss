import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";

// Calls to the attachments API (/v1/attachments): list the signed-in user's
// files and upload new ones. Responses come wrapped in the API's payload
// envelope; upload rejections carry a dotted keyword like DOC.UPLOAD.INFECTED.
//
// These endpoints are not in the generated client yet, hence the raw fetches
// with authHeaders(). Swap for the SDK after regenerating the schema.

export interface AttachmentPayload {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: string;
  submissionId?: string | null;
  uploadedAt: string;
}

/**
 * An upload the API refused: HTTP status, the error keyword when the response
 * carried one, and the server's detail text as the message.
 */
export class AttachmentUploadError extends Error {
  keyword?: string;
  status: number;

  constructor(status: number, keyword?: string, detail?: string) {
    super(detail ?? `Upload failed (${status})`);
    this.name = "AttachmentUploadError";
    this.status = status;
    this.keyword = keyword;
  }
}

/** The caller's released attachments, newest first. */
export async function listAttachments(): Promise<AttachmentPayload[]> {
  const res = await fetch(`${API_URL}/v1/attachments`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Attachments fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/** Uploads one file as the multipart "file" field. */
export async function uploadAttachment(file: File): Promise<AttachmentPayload> {
  const form = new FormData();
  form.append("file", file);
  // No Content-Type header here: the browser must set the multipart boundary
  // itself.
  const res = await fetch(`${API_URL}/v1/attachments`, {
    method: "POST",
    headers: authHeaders(),
    body: form,
  });
  if (!res.ok) {
    let keyword: string | undefined;
    let detail: string | undefined;
    try {
      const problem = await res.json();
      keyword = problem.keyword;
      detail = problem.detail;
    } catch {
      // Not a ProblemDetails body; the status alone will have to do.
    }
    throw new AttachmentUploadError(res.status, keyword, detail);
  }
  return (await res.json()).payload;
}
