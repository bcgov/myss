import { API_URL } from "@/constants";
import { authHeaders } from "@/auth/accessToken";
import { readValidationErrors, type FormValidationError } from "@/api/forms";
import type { PhoneType } from "@/lib/phone";

// Calls to the account API (/v1/account): the citizen's Account Info
// (MYSS-271). Always the caller's own account. Responses come wrapped in the
// API's payload envelope.
//
// Not in the generated client yet, hence the raw fetches with authHeaders().
// Swap for the SDK after regenerating the schema.

/** A stored phone number: ten digits, formatting stripped. */
export interface AccountPhone {
  number: string;
  type: PhoneType;
}

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
}

/** A phone as sent: the number as typed, punctuation allowed. */
export interface PhoneInput {
  number: string;
  type: PhoneType;
}

/**
 * A refused account call. A 422 carries every field error at once, keyed
 * `phones[i].number` / `phones[i].type`; anything else (403 without a
 * profile, 401, 500) has none and the message is all there is.
 */
export class AccountRequestError extends Error {
  readonly status: number;
  readonly errors: readonly FormValidationError[];

  constructor(status: number, errors: readonly FormValidationError[]) {
    super(
      errors.length > 0
        ? errors.map((error) => error.message).join(" ")
        : `Account request failed (${status})`,
    );
    this.name = "AccountRequestError";
    this.status = status;
    this.errors = errors;
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
    const errors = res.status === 422 ? await readValidationErrors(res) : [];
    throw new AccountRequestError(res.status, errors);
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

export function updateNotificationPreferences(
  monthlyReportReminder: boolean,
): Promise<AccountPayload> {
  return send("/notification-preferences", {
    method: "PUT",
    body: JSON.stringify({ monthlyReportReminder }),
  });
}
