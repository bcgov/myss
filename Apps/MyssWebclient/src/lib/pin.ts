// The 4-digit PIN (MYSS-258): a Basic BCeID citizen's electronic signature
// credential, created at registration and changed on Account Info. Mirrors
// Myss.Api.Domain.Pin, and both are driven by the "pin" vectors in
// Shared/validation/validation-vectors.json (pin.unit.test.ts), so a mistake
// is caught before the round trip and the API, which re-checks everything,
// agrees. Deliberately not a Form.io rule: registration answers are stored as
// submitted, so a PIN must never be a form field.

import type { FormValidationError } from "@/api/forms";
import { catalogueMessage } from "@/lib/errorCatalogue";

export const PIN_KEYWORDS = {
  invalidFormat: "IDA.PIN.INVALID_FORMAT",
  mismatch: "IDA.PIN.MISMATCH",
} as const;

/**
 * The compiled wording for the keywords above. MUST stay identical to
 * Shared/validation/error-messages.json, which the unit test checks; the
 * catalogue the Service Designer publishes replaces it at run time.
 */
export const PIN_DEFAULT_MESSAGES: Readonly<Record<string, string>> = {
  [PIN_KEYWORDS.invalidFormat]: "Enter a 4-digit PIN using numbers only.",
  [PIN_KEYWORDS.mismatch]: "The two PINs do not match.",
};

/** The wording a citizen reads for a PIN keyword: the catalogue's, else ours. */
export function pinMessage(keyword: string): string {
  return catalogueMessage(keyword) ?? PIN_DEFAULT_MESSAGES[keyword] ?? keyword;
}

/** Exactly four ASCII digits. Nothing is stripped: a PIN is typed, not pasted. */
export function isPin(value: string): boolean {
  return /^[0-9]{4}$/.test(value);
}

/** The registration request's PIN fields, which the API reports errors by. */
export const REGISTRATION_PIN_FIELDS = {
  pin: "pin",
  confirmation: "pinConfirmation",
} as const;

/** The keyword a candidate PIN fails with, or null when it is a PIN. */
export function checkPin(value: string): string | null {
  return isPin(value) ? null : PIN_KEYWORDS.invalidFormat;
}

/**
 * A new PIN and its confirmation, checked the way the API checks them: the
 * PIN's format first, and the match only once the PIN itself is valid, so the
 * citizen reads one reason per field.
 */
export function checkNewPin(
  pin: string,
  confirmation: string,
  fields: { pin: string; confirmation: string },
): FormValidationError[] {
  const formatKeyword = checkPin(pin);
  if (formatKeyword) {
    return [
      {
        field: fields.pin,
        keyword: formatKeyword,
        message: pinMessage(formatKeyword),
      },
    ];
  }
  if (confirmation !== pin) {
    return [
      {
        field: fields.confirmation,
        keyword: PIN_KEYWORDS.mismatch,
        message: pinMessage(PIN_KEYWORDS.mismatch),
      },
    ];
  }
  return [];
}
