import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  FormMessageLoadError,
  FormMessageRejectedError,
  getFormMessage,
  listFormMessages,
  saveFormMessage,
  type SaveFormMessageInput,
} from "@/api/formMessages";

// The form message pages' reads and save, against MyssAdminApi. No stale time:
// the pages always show the messages as stored.

const LIST_KEY = ["form-messages"] as const;

function messageKeyOf(formSpecId: string, messageKey: string) {
  return ["form-message", formSpecId, messageKey] as const;
}

// A refused account or a missing message will not change on a second try;
// an outage might.
function retryOutageOnce(failureCount: number, error: Error): boolean {
  const refused = error instanceof FormMessageLoadError && error.status < 500;
  return !refused && failureCount < 1;
}

/** Every defined message with its last change. */
export function useFormMessages() {
  return useQuery({
    queryKey: LIST_KEY,
    queryFn: listFormMessages,
    retry: retryOutageOnce,
  });
}

/** One message's definition, live entry and history. */
export function useFormMessage(formSpecId: string, messageKey: string) {
  return useQuery({
    queryKey: messageKeyOf(formSpecId, messageKey),
    queryFn: () => getFormMessage(formSpecId, messageKey),
    retry: retryOutageOnce,
  });
}

/**
 * Saves a message's new texts. The saved message replaces the cached one. When
 * the stored message may differ from the cached one (409, 5xx, no response),
 * it is read again.
 */
export function useSaveFormMessage(formSpecId: string, messageKey: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: SaveFormMessageInput) =>
      saveFormMessage(formSpecId, messageKey, input),
    onSuccess: (saved) => {
      queryClient.setQueryData(messageKeyOf(formSpecId, messageKey), saved);
      void queryClient.invalidateQueries({ queryKey: LIST_KEY });
    },
    onError: (error) => {
      const unchanged =
        error instanceof FormMessageRejectedError &&
        [401, 403, 422].includes(error.status);
      if (!unchanged) {
        void queryClient.invalidateQueries({
          queryKey: messageKeyOf(formSpecId, messageKey),
        });
        void queryClient.invalidateQueries({ queryKey: LIST_KEY });
      }
    },
  });
}
