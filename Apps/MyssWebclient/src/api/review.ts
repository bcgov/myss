import type { JSON as FormioJson } from "@formio/react/lib/components/Form";

import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";
import type { FormSpecPayload } from "@/api/forms";
import { ApplicationRequestError, type ApplicationStatus } from "@/api/intake";

// Calls to the review API (/v1/intake/review/applications): the worker side
// of Application Intake. List every submitted application, read one with the
// answers the applicant submitted and the archived spec they were rendered
// under, pick it up (Under Review, assigned to the caller), add internal
// notes, and decide it: return it for more information, escalate, deny or
// accept, or release it back to the queue. Responses come wrapped in the
// API's payload envelope.
//
// Not in the generated client yet, hence the raw fetches with authHeaders().
// Swap for the SDK after regenerating the schema.
//
// Design: docs/development_design/income-assistance-application.md, "Worker
// slice (MYSS-226)".

/**
 * What a worker may do to a file next, computed server-side from its state
 * and the caller: anyone may pick up a queued file (Review); only the
 * assignee may do the rest.
 */
export type ReviewAction =
  "Review" | "RequestMoreInfo" | "Escalate" | "Deny" | "Accept" | "Release";

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
  /** The IDIR of the worker who picked it up, while it is Under Review. */
  assignee?: string | null;
  /** Whether it is waiting in the queue: Submitted, Re-submitted or Returned. */
  inQueue: boolean;
}

/** One internal note. Workers only; the applicant never sees these. */
export interface ReviewNotePayload {
  text: string;
  /** The IDIR of the worker who wrote it. */
  author: string;
  createdAt: string;
}

/** What happened to the file, in the API's codes. */
export type ReviewHistoryCode =
  | "SUBMIT"
  | "RESUBMIT"
  | "PICK_UP"
  | "RELEASE"
  | "REQUEST_MORE_INFO"
  | "ESCALATE"
  | "ACCEPT"
  | "DENY"
  | "PROMOTED"
  | "PROMOTION_FAILED";

/** One entry of the file's history, oldest first. */
export interface ReviewHistoryEntryPayload {
  code: ReviewHistoryCode;
  actorKind: "APPLICANT" | "WORKER" | "SYSTEM";
  /** The worker's IDIR, when a worker did it. */
  idir?: string | null;
  occurredAt: string;
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
  /** Whether the caller may add a note: the assignee of a file Under Review. */
  canAddNote: boolean;
  notes: ReviewNotePayload[];
  history: ReviewHistoryEntryPayload[];
}

/** The keywords the review API returns on problem-details responses. */
export const REVIEW_KEYWORDS = {
  /** The file's state does not allow that action (409). */
  notAllowed: "INTAKE.REVIEW.NOT_ALLOWED",
  /** The stream version sent is stale: another worker acted first (409). */
  eventConflict: "PLATFORM.EVENTSTORE.CONFLICT",
  /** Another worker has the file Under Review; only they can act (409). */
  assignedToAnother: "INTAKE.REVIEW.ASSIGNED_TO_ANOTHER",
} as const;

/** The route segment for each action, as the API names them. */
export type ReviewActionRoute =
  "review" | "request-more-info" | "escalate" | "deny" | "accept" | "release";

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
 * Moves the file: `review` picks it up (Under Review, assigned to the
 * caller), `release` puts it back, the rest decide it. The server appends the
 * matching event expecting the stream to be at `streamVersion`; a stale one,
 * a state that does not allow the action, or a file another worker holds is
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

/**
 * Adds an internal note to a file the caller has Under Review. Same version
 * guard as an action; a blank note is a 400. The returned application carries
 * no spec.
 */
export async function addReviewNote(
  id: string,
  text: string,
  streamVersion: number,
): Promise<ReviewApplicationPayload> {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(id)}/notes`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify({ streamVersion, text }),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}
