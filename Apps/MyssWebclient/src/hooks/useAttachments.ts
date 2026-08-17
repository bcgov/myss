import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";

// Mirrors AttachmentsController. Same raw-fetch caveat as usePocForm: the
// Bearer interceptor only wraps the generated client, so every call here
// carries authHeaders() explicitly. Regenerate the schema and move to the SDK
// once the attachments API settles.

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

export function useAttachments() {
  return useQuery({
    queryKey: ["attachments"],
    queryFn: async (): Promise<AttachmentPayload[]> => {
      const res = await fetch(`${API_URL}/v1/attachments`, {
        headers: authHeaders(),
      });
      if (!res.ok) throw new Error(`Attachments fetch failed (${res.status})`);
      return (await res.json()).payload;
    },
  });
}

export function useUploadAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (file: File): Promise<AttachmentPayload> => {
      const form = new FormData();
      form.append("file", file);
      // No Content-Type header here: the browser must set the multipart
      // boundary itself.
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
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["attachments"] }),
  });
}
