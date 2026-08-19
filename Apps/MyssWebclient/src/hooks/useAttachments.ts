import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { listAttachments, uploadAttachment } from "@/api/attachments";

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
