import type { JSON as FormioJson } from "@formio/react/lib/components/Form";

import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";
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

/** The application's state, folded server-side from its event stream. */
export type ApplicationStatus = "DRAFT" | "SUBMITTED";

/** Mirrors the intake ApplicationModel in MyssApi (Myss.Api.Intake). */
export interface ApplicationPayload {
  id: string;
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
  /** The application is no longer a draft (409). */
  notEditable: "INTAKE.APPLICATION.NOT_EDITABLE",
  /** The caller has no registered MySS profile (403). */
  profileRequired: "INTAKE.APPLICATION.PROFILE_REQUIRED",
  /** Two submits raced on the event stream (409). */
  eventConflict: "PLATFORM.EVENTSTORE.CONFLICT",
} as const;

/**
 * A non-422 refusal from the intake API: a ProblemDetails body carrying the
 * stable `keyword`, and on a 409 the `currentVersion` the row is at now so
 * the caller can refetch and retry against it. Also thrown for reads that
 * fail (404 for a missing or someone else's application, 401, 500).
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

    // See SubmissionRejectedError: keep `instanceof` reliable after downlevel.
    Object.setPrototypeOf(this, ApplicationRequestError.prototype);
  }

  /** True when the write lost to another tab and the row has moved on. */
  get isConflict(): boolean {
    return this.status === 409;
  }
}

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
  const problem = await readProblem(res);
  throw new ApplicationRequestError(
    res.status,
    problem.keyword,
    problem.currentVersion,
    problem.detail,
  );
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
 * Saves the draft's working copy. The server ignores required-field errors on
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
 * Submits the application. The server re-validates every answer against the
 * pinned spec (422 with every reason), checks the row version (409), and
 * appends the Submitted event; the returned application is read-only.
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
