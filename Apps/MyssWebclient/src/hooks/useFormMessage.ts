import { useQuery } from "@tanstack/react-query";
import type { FormMessageTexts } from "@myss/web-common/api";

import { getFormMessage } from "@/api/formMessages";

/**
 * A form message's live texts once they arrive, otherwise `builtIn`. Read
 * again each time the form opens, but not when the window regains focus.
 */
export function useFormMessage(
  formSpecId: string,
  messageKey: string,
  builtIn: Readonly<FormMessageTexts>,
): Readonly<FormMessageTexts> {
  const { data } = useQuery({
    queryKey: ["form-message", formSpecId, messageKey],
    queryFn: () => getFormMessage(formSpecId, messageKey),
    retry: 1,
    refetchOnWindowFocus: false,
  });
  return data ?? builtIn;
}
