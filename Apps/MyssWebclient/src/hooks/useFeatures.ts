import { useQuery } from "@tanstack/react-query";

import { getFeatures, type FeatureName } from "@/api/features";

export type { FeatureName, Features } from "@/api/features";

/** Flags change only with a redeploy or a config change; refresh every few minutes. */
const FEATURES_STALE_TIME = 5 * 60 * 1000;

/** Every feature flag (anonymous read). */
export function useFeatures() {
  return useQuery({
    queryKey: ["features"],
    queryFn: getFeatures,
    staleTime: FEATURES_STALE_TIME,
  });
}

/**
 * Whether one feature is on: `undefined` while the flags load. When they
 * cannot be read the feature counts as on; the API still refuses a switched
 * off feature, and hiding the whole app over a failed read would be worse.
 */
export function useFeature(name: FeatureName): boolean | undefined {
  const { data, isError } = useFeatures();
  if (isError) return true;
  return data?.[name];
}
