import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { listAttachments, uploadAttachment } from "@/api/attachments";

// Service layer for the attachments module: react-query wiring only. The
// transport (URLs, auth, envelope, errors) lives in @/api/attachments.

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
