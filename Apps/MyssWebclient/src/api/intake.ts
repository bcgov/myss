import type { JSON as FormioJson } from "@formio/react/lib/components/Form";
import {
  ApplicationRequestError,
  throwApplicationRequestError,
  type ApplicationStatus,
} from "@myss/web-common/api";
import { authHeaders } from "@myss/web-common/auth";

import { API_URL } from "@/constants";
import {
  readValidationErrors,
  SubmissionRejectedError,
  type FormSpecPayload,
} from "@/api/forms";

// Calls to the intake API (/v1/intake/applications): the citizen's Income
// Assistance applications. Create a draft, list mine, read one with the
// archived spec it renders under, save the draft answers, submit. Responses
// come wrapped in the API's payload envelope.
//
// Not in the generated client yet, hence the raw fetches with authHeaders().
// Swap for the SDK after regenerating the schema.
//
// Design: docs/development_design/income-assistance-application.md.

// The status and the problem-details refusal are shared with the admin
// webclient's review calls (@myss/web-common/api); re-exported here for this
// app's callers. The applicant edits a DRAFT, and a RETURNED application once
// a worker asks for more information; every other status is shown to them
// read-only.
export { ApplicationRequestError, type ApplicationStatus };

/** Whether the applicant may change the answers: a draft, or a returned application. */
export function isEditableStatus(status: ApplicationStatus): boolean {
  return status === "DRAFT" || status === "RETURNED";
}

/** Mirrors the intake ApplicationModel in MyssApi (Myss.Api.Intake). */
export interface ApplicationPayload {
  id: string;
  /**
   * The request number shown to the applicant at submission and to workers
   * in their list: derived server-side from the id (IA-XXXXXXXX), the same
   * string on both sides.
   */
  referenceNumber: string;
  status: ApplicationStatus;
  /**
   * The answers row version, incremented on every save. Sent back on save and
   * submit so the tab holding the latest version is the one that can write;
   * a stale one gets a 409.
   */
  version: number;
  formSpecId: string;
  formSpecVersion: number;
  answers: { [key: string]: FormioJson };
  createdAt: string;
  updatedAt: string;
  submittedAt?: string | null;
  /** The archived spec at the pinned version. Present on a read by id only. */
  spec?: FormSpecPayload | null;
}

/** A row of the citizen's applications list: no answers, no spec. */
export type ApplicationSummaryPayload = Omit<
  ApplicationPayload,
  "answers" | "spec"
>;

/** What the client sends on save and on submit. */
export interface AnswersInput {
  version: number;
  answers: Record<string, unknown>;
}

/** The keywords the intake API returns on problem-details responses. */
export const INTAKE_KEYWORDS = {
  /** The row version sent is stale: another tab saved first (409). */
  conflict: "INTAKE.APPLICATION.CONFLICT",
  /** The application is neither a draft nor returned for more information (409). */
  notEditable: "INTAKE.APPLICATION.NOT_EDITABLE",
  /** The caller has no registered MySS profile (403). */
  profileRequired: "INTAKE.APPLICATION.PROFILE_REQUIRED",
  /** Two submits raced on the event stream (409). */
  eventConflict: "PLATFORM.EVENTSTORE.CONFLICT",
} as const;

/**
 * Turns a failed response into the typed error the callers branch on: 422 is
 * the forms validation shape (every field error at once), anything else a
 * problem-details refusal.
 */
async function throwFor(res: Response): Promise<never> {
  if (res.status === 422) {
    throw new SubmissionRejectedError(
      res.status,
      await readValidationErrors(res),
    );
  }
  return throwApplicationRequestError(res);
}

const BASE_URL = `${API_URL}/v1/intake/applications`;

/** Creates a new draft pinned to the latest published spec version. */
export async function createApplication(): Promise<ApplicationPayload> {
  const res = await fetch(BASE_URL, {
    method: "POST",
    headers: authHeaders(),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/** The caller's applications, newest first. */
export async function listApplications(): Promise<ApplicationSummaryPayload[]> {
  const res = await fetch(BASE_URL, { headers: authHeaders() });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/** One application with the archived spec it renders under. */
export async function getApplication(id: string): Promise<ApplicationPayload> {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(id)}`, {
    headers: authHeaders(),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/**
 * Saves the working copy of a draft or a returned application. The server ignores required-field errors on
 * a save (a partial draft is the point), so a 422 here means an unknown key
 * or a wrong type, not an empty field.
 */
export async function saveAnswers(
  id: string,
  input: AnswersInput,
): Promise<ApplicationPayload> {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(id)}/answers`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}

/**
 * Submits the application, or resubmits a returned one. The server
 * re-validates every answer against the pinned spec (422 with every reason),
 * checks the row version (409), and appends the Submitted (or Resubmitted)
 * event; the returned application is read-only.
 */
export async function submitApplication(
  id: string,
  input: AnswersInput,
): Promise<ApplicationPayload> {
  const res = await fetch(`${BASE_URL}/${encodeURIComponent(id)}/submit`, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok) await throwFor(res);
  return (await res.json()).payload;
}
