import type { JSON as FormioJson } from "@formio/react/lib/components/Form";

import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";
import type { FormSpecPayload } from "@/api/forms";
import { ApplicationRequestError, type ApplicationStatus } from "@/api/intake";

// Calls to the review API (/v1/intake/review/applications): the worker side
// of Application Intake. List every submitted application, read one with the
// answers the applicant submitted and the archived spec they were rendered
// under, and move it through Under Review to Accepted or Denied. Responses
// come wrapped in the API's payload envelope.
//
// Not in the generated client yet, hence the raw fetches with authHeaders().
// Swap for the SDK after regenerating the schema.
//
// Design: docs/development_design/income-assistance-application.md, "Worker
// slice (MYSS-226)".

/** What a worker may do to a file next, computed server-side from its state. */
export type ReviewAction = "Review" | "Accept" | "Deny";

/** A row of the worker's list: the submitted applications, newest first. */
export interface ReviewApplicationSummaryPayload {
  id: string;
  /** The request number the applicant was given at submission. */
  referenceNumber: string;
  status: ApplicationStatus;
  submittedAt: string;
  /**
   * The event stream version the file is at. Every action sends it back so
   * two workers cannot both act on the same version: the second gets a 409.
   */
  streamVersion: number;
}

/** One application opened for review. Mirrors ReviewApplicationModel. */
export interface ReviewApplicationPayload extends ReviewApplicationSummaryPayload {
  /** The answers as submitted, from the event log. */
  answers: { [key: string]: FormioJson };
  formSpecId: string;
  formSpecVersion: number;
  /** The archived spec at the pinned version. Present on a read by id only. */
  spec?: FormSpecPayload | null;
  availableActions: ReviewAction[];
}

/** The keywords the review API returns on problem-details responses. */
export const REVIEW_KEYWORDS = {
  /** The file's state does not allow that action (409). */
  notAllowed: "INTAKE.REVIEW.NOT_ALLOWED",
  /** The stream version sent is stale: another worker acted first (409). */
  eventConflict: "PLATFORM.EVENTSTORE.CONFLICT",
} as const;

/** The route segment for each action, as the API names them. */
export type ReviewActionRoute = "review" | "accept" | "deny";

const BASE_URL = `${API_URL}/v1/intake/review/applications`;

async function readProblem(res: Response): Promise<{
  keyword?: string;
  currentVersion?: number;
  detail?: string;
}> {
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

/** Every review refusal is a problem-details body with a stable keyword. */
async function throwFor(res: Response): Promise<never> {
  const problem = await readProblem(res);
  throw new ApplicationRequestError(
    res.status,
    problem.keyword,
    problem.currentVersion,
    problem.detail,
  );
}

/** Every submitted application, newest submission first. Drafts never appear. */
export async function listReviewQueue(): Promise<
  ReviewApplicationSummaryPayload[]
> {
  const res = await fetch(BASE_URL, { headers: authHeaders() });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/** One submitted application with its archived spec and available actions. */
export async function getReviewApplication(
  id: string,
): Promise<ReviewApplicationPayload> {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(id)}`, {
    headers: authHeaders(),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/**
 * Moves the file: `review` marks it Under Review, `accept` and `deny` decide
 * it. The server appends the matching event expecting the stream to be at
 * `streamVersion`; a stale one, or a state that does not allow the action, is
 * a 409. The returned application carries no spec.
 */
export async function performReviewAction(
  id: string,
  action: ReviewActionRoute,
  streamVersion: number,
): Promise<ReviewApplicationPayload> {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(id)}/${action}`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify({ streamVersion }),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}
