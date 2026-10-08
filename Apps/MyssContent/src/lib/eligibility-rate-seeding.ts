import { jsonEqual } from "./json-equal";
import type { EligibilityRateSeed } from "./eligibility-rate-seed-data";

/** What the bootstrap does with one seeded rate table. */
export type SeedRateAction =
  /** No table has the seeded date: create and publish it. */
  | "create"
  /** The published table equals the seed. */
  | "keep"
  /** The published table differs from the seed, for example after an admin edit. */
  | "keep-changed"
  /** A table with the seeded date exists but is not published, so it is not served. */
  | "keep-draft-only";

/** The stored version of a rate table, as the Document Service returns it. */
export interface StoredRateTable {
  readonly incomeRows?: unknown;
  readonly assetLimits?: unknown;
}

/**
 * Rate seeding is create-only: an existing table is never changed, so an
 * admin's edit survives a restart. `draft` and `published` are the stored
 * versions of the document with the seed's effective date, if any.
 */
export function seedRateAction(
  seed: EligibilityRateSeed,
  draft: StoredRateTable | null | undefined,
  published: StoredRateTable | null | undefined,
): SeedRateAction {
  if (!draft && !published) return "create";
  if (!published) return "keep-draft-only";
  return jsonEqual(published.incomeRows, seed.incomeRows) &&
    jsonEqual(published.assetLimits, seed.assetLimits)
    ? "keep"
    : "keep-changed";
}
