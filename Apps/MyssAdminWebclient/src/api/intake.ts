// The two intake types the review screens share with the citizen webclient's
// api/intake.ts (copied, not shared: the review API answers with the same
// status codes and the same ProblemDetails refusals). The citizen-only calls
// (create, list mine, save answers, submit) stay in the citizen app.

/** The application's state, folded server-side from its event stream. */
export type ApplicationStatus =
  | "DRAFT"
  | "SUBMITTED"
  | "UNDER_REVIEW"
  | "RETURNED"
  | "RESUBMITTED"
  | "ESCALATED"
  | "ACCEPTED"
  | "DENIED"
  | "PROMOTION_FAILED";

/**
 * A non-422 refusal from the intake API: a ProblemDetails body carrying the
 * stable `keyword`, and on a 409 the `currentVersion` the row is at now so
 * the caller can refetch and retry against it. Also thrown for reads that
 * fail (404 for a missing application, 401, 403, 500).
 */
export class ApplicationRequestError extends Error {
  readonly status: number;
  readonly keyword?: string;
  readonly currentVersion?: number;

  constructor(
    status: number,
    keyword?: string,
    currentVersion?: number,
    detail?: string,
  ) {
    super(detail ?? `Application request failed (${status})`);
    this.name = "ApplicationRequestError";
    this.status = status;
    this.keyword = keyword;
    this.currentVersion = currentVersion;

    // Keep `instanceof` reliable after downlevel compilation.
    Object.setPrototypeOf(this, ApplicationRequestError.prototype);
  }

  /** True when the write lost to another tab and the row has moved on. */
  get isConflict(): boolean {
    return this.status === 409;
  }
}
