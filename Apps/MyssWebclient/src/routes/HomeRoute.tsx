import { useEffect, type ReactNode } from "react";

import { LEGACY_MYSS_URL } from "@/constants";
import { useFeature } from "@/hooks/useFeatures";
import { leaveSite } from "@/routes/leaveSite";

/**
 * The home page while its HomePage flag is on (MYSS-329). When an environment
 * switches it off, a visitor to `/` goes to legacy MySelfServe instead. That
 * includes a switched off feature's route, since FeatureRoute sends it home.
 * Renders nothing while the flags load or the browser leaves, so the home
 * page never flashes first.
 */
export default function HomeRoute({
  children,
}: Readonly<{ children: ReactNode }>) {
  const enabled = useFeature("HomePage");

  useEffect(() => {
    if (enabled === false) leaveSite(LEGACY_MYSS_URL);
  }, [enabled]);

  if (!enabled) return null;
  return children;
}
