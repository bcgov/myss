import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";
import { readValidationErrors, type FormValidationError } from "@/api/forms";
import type { PhoneType } from "@/lib/phone";

// Calls to the account API (/v1/account): the citizen's Account Info
// (MYSS-271) and PIN (MYSS-258). Always the caller's own account. Responses come wrapped in the
// API's payload envelope.
//
// Not in the generated client yet, hence the raw fetches with authHeaders().
// Swap for the SDK after regenerating the schema.

/** A stored phone number: ten digits, formatting stripped. */
export interface AccountPhone {
  number: string;
  type: PhoneType;
}

/**
 * Where the citizen stands with the PIN (MYSS-258): only Basic BCeID users
 * have one; a NotSet one registered before PINs were asked for.
 */
export type AccountPinStatus = "NotApplicable" | "NotSet" | "Set";

/** Mirrors AccountModel in MyssApi/Models/AccountModels.cs. */
export interface AccountPayload {
  /** The ICM case number; a placeholder until ICM is connected. */
  caseNumber?: string | null;
  clientName: string;
  /** Other people on the case; a placeholder until MIS is connected. */
  familyMembers: string[];
  /** From registration. Read-only on this page. */
  email: string;
  phones: AccountPhone[];
  /** One line per entry; a placeholder until MIS is connected. */
  mailingAddressLines: string[];
  monthlyReportReminder: boolean;
  pinStatus: AccountPinStatus;
}

/** A phone as sent: the number as typed, punctuation allowed. */
export interface PhoneInput {
  number: string;
  type: PhoneType;
}

/** The ProblemDetails the API refuses with outside a 422. */
interface AccountProblem {
  detail?: string;
  keyword?: string;
}

/**
 * A refused account call. A 422 carries every field error at once, keyed
 * `phones[i].number` / `phones[i].type` or `currentPin` / `newPin` /
 * `confirmPin`. Anything else (403 without a profile, 429 while the PIN is
 * locked, 401, 500) has none; the API's own detail, when it sends one, is
 * the message, and its keyword says which refusal it was.
 */
export class AccountRequestError extends Error {
  readonly status: number;
  readonly errors: readonly FormValidationError[];
  readonly keyword?: string;

  constructor(
    status: number,
    errors: readonly FormValidationError[],
    problem: AccountProblem = {},
  ) {
    super(
      errors.length > 0
        ? errors.map((error) => error.message).join(" ")
        : (problem.detail ?? `Account request failed (${status})`),
    );
    this.name = "AccountRequestError";
    this.status = status;
    this.errors = errors;
    this.keyword = problem.keyword;
  }
}

/** The keyword of a 429 while Change PIN is locked after wrong PINs. */
export const PIN_LOCKED_KEYWORD = "ACCOUNT.PIN.LOCKED";

async function readProblem(res: Response): Promise<AccountProblem> {
  try {
    const body = (await res.json()) as AccountProblem;
    return {
      detail: typeof body.detail === "string" ? body.detail : undefined,
      keyword: typeof body.keyword === "string" ? body.keyword : undefined,
    };
  } catch {
    // A proxy's HTML error page, or no body at all.
    return {};
  }
}

async function send(path: string, init?: RequestInit): Promise<AccountPayload> {
  const res = await fetch(`${API_URL}/v1/account${path}`, {
    ...init,
    headers: {
      ...authHeaders(),
      ...(init?.body ? { "Content-Type": "application/json" } : {}),
    },
  });
  if (!res.ok) {
    if (res.status === 422) {
      throw new AccountRequestError(422, await readValidationErrors(res));
    }
    throw new AccountRequestError(res.status, [], await readProblem(res));
  }
  const body = (await res.json()) as { payload: AccountPayload };
  return body.payload;
}

export function getAccount(): Promise<AccountPayload> {
  return send("");
}

/** Replaces the whole list; an empty list removes every number. */
export function updatePhones(phones: PhoneInput[]): Promise<AccountPayload> {
  return send("/phones", {
    method: "PUT",
    body: JSON.stringify({ phones }),
  });
}

/** The body of a PIN save. `currentPin` is only needed to change one. */
export interface PinInput {
  currentPin?: string;
  newPin: string;
  confirmPin: string;
}

/** Changes the PIN, or creates it when there is none. Basic BCeID only. */
export function savePin(input: PinInput): Promise<AccountPayload> {
  return send("/pin", {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function updateNotificationPreferences(
  monthlyReportReminder: boolean,
): Promise<AccountPayload> {
  return send("/notification-preferences", {
    method: "PUT",
    body: JSON.stringify({ monthlyReportReminder }),
  });
}
