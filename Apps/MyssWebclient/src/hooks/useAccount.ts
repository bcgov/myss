import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  AccountRequestError,
  getAccount,
  updateNotificationPreferences,
  updatePhones,
  type AccountPayload,
  type PhoneInput,
} from "@/api/account";

// React-query hooks for the citizen's Account Info (MYSS-271). Raw fetches
// for now (see api/account.ts), so every call carries authHeaders() itself.

export { AccountRequestError } from "@/api/account";
export type { AccountPayload, AccountPhone, PhoneInput } from "@/api/account";

export const ACCOUNT_QUERY_KEY = ["account"] as const;

// A 4xx (403 without a profile, 401) is a stable answer, not a transient
// fault; retrying only delays the message.
function shouldRetry(failureCount: number, error: Error): boolean {
  if (
    error instanceof AccountRequestError &&
    error.status >= 400 &&
    error.status < 500
  ) {
    return false;
  }
  return failureCount < 2;
}

/** The caller's account. */
export function useAccount() {
  return useQuery({
    queryKey: ACCOUNT_QUERY_KEY,
    queryFn: getAccount,
    retry: shouldRetry,
  });
}

// Every write answers with the account as it now stands, so the cache takes
// the response as is instead of refetching.
function useAccountWrite<TInput>(
  write: (input: TInput) => Promise<AccountPayload>,
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: write,
    onSuccess: (account) => {
      queryClient.setQueryData(ACCOUNT_QUERY_KEY, account);
    },
  });
}

/** Replaces the caller's phone numbers. */
export function useUpdatePhones() {
  return useAccountWrite((phones: PhoneInput[]) => updatePhones(phones));
}

/** Saves the monthly report reminder. */
export function useUpdateNotificationPreferences() {
  return useAccountWrite((monthlyReportReminder: boolean) =>
    updateNotificationPreferences(monthlyReportReminder),
  );
}
