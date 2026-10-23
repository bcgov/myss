import type {
  EligibilityAssetLimits,
  EligibilityRateRow,
  EligibilityRates,
} from "@myss/web-common/api";
import { authHeaders } from "@myss/web-common/auth";

import { API_URL } from "@/constants";
import { readValidationErrors, type FormValidationError } from "@/api/forms";

// The rates editor's calls to MyssAdminApi: read the current table and save a
// new one (GET and POST /v1/EligibilityRates). Both need an IDIR sign-in. The
// read goes to the admin API rather than the citizen estimator endpoint so the
// editor always sees the table as stored, never a cached copy.

// The rate table's shape is shared with the citizen estimator
// (@myss/web-common/api); re-exported here for this app's callers.
export type { EligibilityRates };

/** A complete table to save. The server assigns the effective date. */
export interface SaveEstimatorRatesInput {
  incomeRows: EligibilityRateRow[];
  assetLimits: EligibilityAssetLimits;
}

/**
 * A save the API refused. `errors` holds the 422 collection (one per cell or
 * list, keyed by `field`); it is empty for other failures (400, 401, 403, 502).
 */
export class RatesRejectedError extends Error {
  readonly status: number;
  readonly errors: readonly FormValidationError[];

  constructor(status: number, errors: readonly FormValidationError[]) {
    super(
      errors.length > 0
        ? errors.map((error) => error.message).join(" ")
        : `Rates save failed (${status})`,
    );
    this.name = "RatesRejectedError";
    this.status = status;
    this.errors = errors;

    // Keeps `instanceof` working when TypeScript downlevels the class.
    Object.setPrototypeOf(this, RatesRejectedError.prototype);
  }
}

/** The current rate table, read from the content engine without a cache. */
export async function getEstimatorRates(): Promise<EligibilityRates> {
  const res = await fetch(`${API_URL}/v1/EligibilityRates`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Rates fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/**
 * Saves and publishes a complete rate table as today's table, and returns it as
 * stored. A refusal throws {@link RatesRejectedError}.
 */
export async function saveEstimatorRates(
  input: SaveEstimatorRatesInput,
): Promise<EligibilityRates> {
  const res = await fetch(`${API_URL}/v1/EligibilityRates`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) {
    throw new RatesRejectedError(res.status, await readValidationErrors(res));
  }
  return (await res.json()).payload;
}
