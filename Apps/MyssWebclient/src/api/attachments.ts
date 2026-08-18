import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";

// Transport layer for the attachments module — the frontend counterpart of a
// backend provider. This is the only place that knows the URLs, the auth
// header, the BaseResponseModel envelope and the ProblemDetails error shape;
// hooks add react-query caching on top and components never see any of it.
//
// Raw fetch for now (so the Bearer interceptor does not apply and authHeaders()
// is spread explicitly). When the attachments API stabilises and the schema is
// regenerated, the SDK swap happens inside this file only.

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
 * Upload rejection carrying the API's stable dotted keyword (e.g.
 * DOC.UPLOAD.INFECTED) so the UI can match on it instead of parsing prose.
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
