import { API_URL } from "@/constants";
import { authHeaders } from "@myss/web-common/auth";

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

/** The types the API accepts, for the file picker. The API re-checks each file. */
export const ATTACHMENT_ACCEPT = "application/pdf,image/png,image/jpeg";

/** The API's size limit, 5 MiB. */
export const ATTACHMENT_MAX_BYTES = 5_242_880;

export const ATTACHMENT_KEYWORDS = {
  empty: "DOC.UPLOAD.EMPTY",
  tooLarge: "DOC.UPLOAD.TOO_LARGE",
  typeNotAllowed: "DOC.UPLOAD.TYPE_NOT_ALLOWED",
  infected: "DOC.UPLOAD.INFECTED",
  scanUnavailable: "DOC.SCAN.UNAVAILABLE",
} as const;

const KEYWORD_MESSAGES: Readonly<Record<string, string>> = {
  [ATTACHMENT_KEYWORDS.empty]: "The selected file is empty.",
  [ATTACHMENT_KEYWORDS.tooLarge]: "The file is too large. The limit is 5 MB.",
  [ATTACHMENT_KEYWORDS.typeNotAllowed]:
    "Only PDF, PNG and JPEG files are accepted.",
  [ATTACHMENT_KEYWORDS.infected]:
    "The file failed the virus scan and was not stored.",
  [ATTACHMENT_KEYWORDS.scanUnavailable]:
    "The virus scanner is unavailable right now. Try again in a few minutes.",
};

/** Display text for an upload keyword, or undefined for one this list does not know. */
export function attachmentErrorMessage(
  keyword: string | undefined,
): string | undefined {
  return keyword === undefined ? undefined : KEYWORD_MESSAGES[keyword];
}

// The first bytes of each accepted type, as the API checks them.
const SIGNATURES: Readonly<Record<string, readonly number[]>> = {
  "application/pdf": [0x25, 0x50, 0x44, 0x46, 0x2d],
  "image/png": [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
  "image/jpeg": [0xff, 0xd8, 0xff],
};

/**
 * The keyword the API would refuse a file with, or null when it would take
 * it: not empty, within the size limit, and an accepted type both by its
 * declared type and by its first bytes. The virus scan happens only in the API.
 */
export async function checkAttachmentFile(file: File): Promise<string | null> {
  if (file.size === 0) return ATTACHMENT_KEYWORDS.empty;
  if (file.size > ATTACHMENT_MAX_BYTES) return ATTACHMENT_KEYWORDS.tooLarge;
  const signature = SIGNATURES[file.type.toLowerCase()];
  if (!signature) return ATTACHMENT_KEYWORDS.typeNotAllowed;
  const head = new Uint8Array(
    await file.slice(0, signature.length).arrayBuffer(),
  );
  return signature.every((byte, i) => head[i] === byte)
    ? null
    : ATTACHMENT_KEYWORDS.typeNotAllowed;
}

/** A file's size as a citizen reads it: "0 bytes", "412 KB", "1.8 MB". */
export function formatFileSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} bytes`;
  if (sizeBytes < 1024 * 1024) return `${Math.round(sizeBytes / 1024)} KB`;
  return `${(sizeBytes / (1024 * 1024)).toFixed(1)} MB`;
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
