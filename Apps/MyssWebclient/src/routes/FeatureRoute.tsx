import type { ReactNode } from "react";
import { Navigate } from "react-router";

import { useFeature, type FeatureName } from "@/hooks/useFeatures";
import { paths } from "@/routes/paths";

/**
 * Renders a route only while its feature flag is on; a switched off feature
 * sends the visitor home. Renders nothing while the flags load, so a page of
 * a switched off feature never flashes.
 */
export default function FeatureRoute({
  feature,
  children,
}: Readonly<{ feature: FeatureName; children: ReactNode }>) {
  const enabled = useFeature(feature);
  if (enabled === undefined) return null;
  if (!enabled) return <Navigate to={paths.home} replace />;
  return children;
}
