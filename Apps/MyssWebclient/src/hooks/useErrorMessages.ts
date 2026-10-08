import { useQuery } from "@tanstack/react-query";
import { useEffect } from "react";

import { getErrorMessages } from "@/api/errorMessages";
import { setErrorCatalogue } from "@/lib/errorCatalogue";

// The error message catalogue, read once per session. The fetch is ANONYMOUS
// (see @/api/errorMessages). Shared content that changes rarely, so it carries
// the same long staleTime as the estimator's spec and rate table. A page never
// waits for it: callers pass `data` (possibly undefined) to errorMessageFor,
// which falls back to the compiled wording until the catalogue arrives.

export { errorMessageFor } from "@/api/errorMessages";
export type { ErrorMessageCatalogue } from "@/api/errorMessages";

/** Content changes rarely and is not per-user, so keep it fresh for an hour. */
const CONTENT_STALE_TIME = 60 * 60 * 1000;

export function useErrorMessages() {
  return useQuery({
    queryKey: ["error-messages"],
    queryFn: getErrorMessages,
    staleTime: CONTENT_STALE_TIME,
  });
}

/**
 * Reads the catalogue and hands it to the Form.io field wrappers, which are
 * not React components and cannot ask for it themselves. Mounted once in App,
 * so every form on every page words its rule failures from the catalogue.
 */
export function useErrorMessageCatalogue() {
  const query = useErrorMessages();
  useEffect(() => {
    setErrorCatalogue(query.data);
  }, [query.data]);
  return query;
}
