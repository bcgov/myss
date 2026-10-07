import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";
import { readValidationErrors, type FormValidationError } from "@/api/forms";
import type {
  EligibilityAssetLimits,
  EligibilityRateRow,
  EligibilityRates,
} from "@/api/eligibility";

// The IDIR-only rates editor's save call (POST /v1/EligibilityRates). Kept apart
// from @/api/eligibility, whose reads are anonymous by design and must never
// carry authHeaders().

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
