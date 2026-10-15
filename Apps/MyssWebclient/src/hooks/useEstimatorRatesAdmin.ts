import { useMutation, useQueryClient } from "@tanstack/react-query";

import { RatesRejectedError, saveEstimatorRates } from "@/api/eligibilityRatesAdmin";
import { ESTIMATOR_RATES_QUERY_KEY } from "@/hooks/useEligibility";

// The rates editor's save. Apart from useEligibility, which holds only the
// anonymous reads.

/**
 * Saves the edited table. The saved table in the response replaces the cached one;
 * it is not refetched, because another API instance may still serve the previous
 * table from its cache. When the outcome is unknown (a 5xx or no response), the
 * cached table is refetched, since the save may have gone through.
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
