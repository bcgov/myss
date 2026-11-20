import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  deleteApplicationAttachment,
  listApplicationAttachments,
  listAttachments,
  uploadApplicationAttachment,
  uploadAttachment,
} from "@/api/attachments";

// React-query hooks over the attachments API: the user's file list and the
// upload mutation. A successful upload refreshes the list.

export function useAttachments() {
  return useQuery({
    queryKey: ["attachments"],
    queryFn: listAttachments,
  });
}

export function useUploadAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: uploadAttachment,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["attachments"] }),
  });
}

export function applicationAttachmentsKey(applicationId: string) {
  return ["attachments", "application", applicationId] as const;
}

/** The supporting documents on one application. */
export function useApplicationAttachments(applicationId: string) {
  return useQuery({
    queryKey: applicationAttachmentsKey(applicationId),
    queryFn: () => listApplicationAttachments(applicationId),
  });
}

export function useUploadApplicationAttachment(applicationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) =>
      uploadApplicationAttachment(applicationId, file),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: applicationAttachmentsKey(applicationId),
      }),
  });
}

export function useDeleteApplicationAttachment(applicationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (attachmentId: string) =>
      deleteApplicationAttachment(applicationId, attachmentId),
    onSettled: () =>
      queryClient.invalidateQueries({
        queryKey: applicationAttachmentsKey(applicationId),
      }),
  });
}
