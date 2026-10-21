// The application shapes the citizen intake API and the staff review API
// share: the folded status, and the problem-details refusal both answer with
// (the same status codes, the same stable keywords).

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
 * A non-422 refusal from the intake or review API: a ProblemDetails body
 * carrying the stable `keyword`, and on a 409 the `currentVersion` the row is
 * at now so the caller can refetch and retry against it. Also thrown for reads
 * that fail (404 for a missing or someone else's application, 401, 403, 500).
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

    // Subclassing a built-in loses the prototype link when TypeScript
    // downlevels the class; restore it so `instanceof` stays reliable.
    Object.setPrototypeOf(this, ApplicationRequestError.prototype);
  }

  /** True when the write lost to another tab and the row has moved on. */
  get isConflict(): boolean {
    return this.status === 409;
  }
}

/** The ProblemDetails fields a refusal is branched on. */
export interface Problem {
  keyword?: string;
  currentVersion?: number;
  detail?: string;
}

/** Reads a ProblemDetails body, or nothing when the body is not one. */
export async function readProblem(res: Response): Promise<Problem> {
  try {
    const problem = await res.json();
    return {
      keyword:
        typeof problem?.keyword === "string" ? problem.keyword : undefined,
      currentVersion:
        typeof problem?.currentVersion === "number"
          ? problem.currentVersion
          : undefined,
      detail: typeof problem?.detail === "string" ? problem.detail : undefined,
    };
  } catch {
    // Not a ProblemDetails body; the status alone will have to do.
    return {};
  }
}

/** Throws the failed response as an {@link ApplicationRequestError}. */
export async function throwApplicationRequestError(
  res: Response,
): Promise<never> {
  const problem = await readProblem(res);
  throw new ApplicationRequestError(
    res.status,
    problem.keyword,
    problem.currentVersion,
    problem.detail,
  );
}
