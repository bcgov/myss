import { API_URL } from "@/constants";

// GET /v1/features — which citizen features are on in this environment
// (MyssApi/Configuration/FeatureFlags.cs). Anonymous: the home page needs it
// before sign-in. The API enforces every flag itself (an endpoint whose flag
// is off answers 404); these values only decide what the browser shows.

/** Mirrors `FeatureFlags.All` in MyssApi; change both together. */
export const FEATURE_NAMES = [
  "PinReset",
  "EligibilityEstimator",
  "BusPass",
  "Notifications",
  "Messages",
  "ServiceRequests",
  "AccountInfo",
  "Intake",
  "Attachments",
  "TechDemos",
] as const;

export type FeatureName = (typeof FEATURE_NAMES)[number];

export type Features = Record<FeatureName, boolean>;

export async function getFeatures(): Promise<Features> {
  const res = await fetch(`${API_URL}/v1/features`);
  if (!res.ok) throw new Error(`Features fetch failed (${res.status})`);
  return (await res.json()).payload;
}
