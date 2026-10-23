import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import {
  getEstimatorRates,
  RatesRejectedError,
  saveEstimatorRates,
} from "@/api/eligibilityRates";

// The rates editor's read and save, both against MyssAdminApi.

/** The cache key of the rate table; a save replaces what it holds. */
export const ESTIMATOR_RATES_QUERY_KEY = ["estimator-rates"] as const;

/**
 * The table changes only when an admin saves, and a save here replaces the cached
 * copy, so an hour matches what the editor used when it lived in the citizen app.
 */
const RATES_STALE_TIME = 60 * 60 * 1000;

/** The current rate table, as stored in the content engine. */
export function useEstimatorRates() {
  return useQuery({
    queryKey: ESTIMATOR_RATES_QUERY_KEY,
    queryFn: getEstimatorRates,
    staleTime: RATES_STALE_TIME,
  });
}

/**
 * Saves the edited table. The saved table in the response replaces the cached one,
 * so the editor shows exactly what was stored without another read. When the outcome
 * is unknown (a 5xx or no response), the table is refetched, since the save may have
 * gone through.
 */
export function useSaveEstimatorRates() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: saveEstimatorRates,
    onSuccess: (saved) => {
      queryClient.setQueryData(ESTIMATOR_RATES_QUERY_KEY, saved);
    },
    onError: (error) => {
      if (!(error instanceof RatesRejectedError) || error.status >= 500) {
        void queryClient.invalidateQueries({ queryKey: ESTIMATOR_RATES_QUERY_KEY });
      }
    },
  });
}
