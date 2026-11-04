import type { JSON as FormioJson } from "@formio/react/lib/components/Form";
import {
  readValidationErrors,
  SubmissionRejectedError,
  throwApplicationRequestError,
  type ApplicationStatus,
  type FormSpecPayload,
} from "@myss/web-common/api";
import { authHeaders } from "@myss/web-common/auth";

import { API_URL } from "@/constants";

// Calls to the worker override API (/v1/intake/worker-override/applications,
// MYSS-321): a worker fills in the simplified Application for Assistance for a
// client who cannot submit their own. Start a draft, open it, save it, submit
// it. Any worker may open any draft. The submit makes the client's worker
// override account and puts the application in the review queue. Responses
// come wrapped in the API's payload envelope.
//
// Raw fetches with authHeaders(), as api/review.ts.

/** One worker override application. Mirrors WorkerOverrideApplicationModel. */
export interface WorkerOverrideApplicationPayload {
  id: string;
  /** The request number the review queue shows. */
  referenceNumber: string;
  status: ApplicationStatus;
  /** The row version a save or submit sends back; a stale one is a 409. */
  version: number;
  formSpecId: string;
  formSpecVersion: number;
  createdAt: string;
  updatedAt: string;
  submittedAt?: string | null;
  /** The draft's answers, or what was submitted. */
  answers: { [key: string]: FormioJson };
  /** The spec at the pinned version. On a start and a read by id only. */
  spec?: FormSpecPayload | null;
  /** The client's worker override account, once submitted. */
  accountId?: string | null;
}

/** The keywords the worker override API returns on problem-details responses. */
export const WORKER_OVERRIDE_KEYWORDS = {
  /** No version of the worker override form is published (404). */
  specNotPublished: "INTAKE.APPLICATION.SPEC_NOT_PUBLISHED",
  /** Another worker, tab or window saved first (409). */
  conflict: "INTAKE.APPLICATION.CONFLICT",
  /** Submitted and not returned, so it cannot change (409). */
  notEditable: "INTAKE.APPLICATION.NOT_EDITABLE",
} as const;

const BASE_URL = `${API_URL}/v1/intake/worker-override/applications`;

function applicationUrl(id: string): string {
  return `${BASE_URL}/${encodeURIComponent(id)}`;
}

/**
 * A 422 carries the field errors, kept whole for the error summary; any other
 * refusal is a problem-details body with a stable keyword.
 */
async function throwFor(res: Response): Promise<never> {
  if (res.status === 422) {
    throw new SubmissionRejectedError(422, await readValidationErrors(res));
  }
  return throwApplicationRequestError(res);
}

/** Starts a draft on the latest published worker override form. */
export async function startWorkerOverride(): Promise<WorkerOverrideApplicationPayload> {
  const res = await fetch(BASE_URL, { method: "POST", headers: authHeaders() });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/** One worker override application with the spec that renders it. */
export async function getWorkerOverride(
  id: string,
): Promise<WorkerOverrideApplicationPayload> {
  const res = await fetch(applicationUrl(id), { headers: authHeaders() });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/** The input of a save or a submit. */
export interface WorkerOverrideAnswersInput {
  version: number;
  answers: Record<string, unknown>;
}

/** Saves a draft's answers, complete or not. The response carries no spec. */
export async function saveWorkerOverrideAnswers(
  id: string,
  input: WorkerOverrideAnswersInput,
): Promise<WorkerOverrideApplicationPayload> {
  const res = await fetch(`${applicationUrl(id)}/answers`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/**
 * Submits: makes the client's worker override account and puts the
 * application in the review queue. The response carries no spec.
 */
export async function submitWorkerOverride(
  id: string,
  input: WorkerOverrideAnswersInput,
): Promise<WorkerOverrideApplicationPayload> {
  const res = await fetch(`${applicationUrl(id)}/submit`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}
