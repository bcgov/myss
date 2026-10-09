// The client half of the validation rule registry. A form field opts into a
// rule with `properties.myssValidator` ("sin", "email", "phone", "postalCode",
// "date", "dateParts") or names the field it must match with
// `properties.myssMatches`; MyssApi's FormSpecValidator reads the same markers
// and runs the same rules, so a value the browser refuses is refused again on
// submit. Both implementations are driven by the vectors in
// Shared/validation/validation-vectors.json (validationRules.unit.test.ts), so
// they cannot drift apart silently.
//
// A rule returns the stable keyword a failure carries and the compiled wording
// for it. The wording a citizen reads is resolved by ruleMessage: per-field
// wording on the form first, then the catalogue, then the compiled text.
// No React, no Form.io: pure functions over strings.

import { catalogueMessage } from "@/lib/errorCatalogue";

/** What a rule needs beyond the value: the other answers and the component. */
export interface RuleContext {
  readonly data: Record<string, unknown>;
  readonly component: Record<string, unknown>;
}

/** A failed rule: which rule, its stable keyword, and the compiled wording. */
export interface RuleFailure {
  readonly rule: string;
  readonly keyword: string;
  readonly message: string;
}

export type ValidationRule = (
  value: string,
  context: RuleContext,
) => RuleFailure | null;

export const RULE_KEYWORDS = {
  sinWrongLength: "IDA.SIN.WRONG_LENGTH",
  sinInvalidChecksum: "IDA.SIN.INVALID_CHECKSUM",
  emailInvalidFormat: "IDA.EMAIL.INVALID_FORMAT",
  emailMismatch: "IDA.EMAIL.MISMATCH",
  phoneInvalidFormat: "IDA.PHONE.INVALID_FORMAT",
  postalCodeInvalidFormat: "IDA.POSTAL_CODE.INVALID_FORMAT",
  dateInvalid: "FORM.DATE.INVALID",
} as const;

/**
 * The compiled wording for each keyword a rule can emit. MUST stay identical
 * to Shared/validation/error-messages.json, which the unit test checks; the
 * catalogue the Service Designer publishes replaces it at run time.
 */
export const DEFAULT_MESSAGES: Readonly<Record<string, string>> = {
  [RULE_KEYWORDS.sinWrongLength]: "A Social Insurance Number must be 9 digits.",
  [RULE_KEYWORDS.sinInvalidChecksum]:
    "That Social Insurance Number is not valid. Check the digits and try again.",
  [RULE_KEYWORDS.emailInvalidFormat]:
    "Enter an email address in the format name@example.com.",
  [RULE_KEYWORDS.emailMismatch]: "The two email addresses do not match.",
  [RULE_KEYWORDS.phoneInvalidFormat]:
    "Enter a 10-digit phone number, for example 250 555 0199.",
  [RULE_KEYWORDS.postalCodeInvalidFormat]:
    "Enter a postal code in the format A1A 1A1.",
  [RULE_KEYWORDS.dateInvalid]: "Enter a valid date.",
};

function failure(rule: string, keyword: string): RuleFailure {
  return { rule, keyword, message: DEFAULT_MESSAGES[keyword] ?? keyword };
}

function digitsOf(value: string): string {
  return value.replace(/\D/g, "");
}

/** Luhn mod-10 over nine digits, doubling every second digit from the left. */
function passesLuhn(digits: string): boolean {
  let total = 0;
  for (let i = 0; i < digits.length; i++) {
    let n = Number(digits[i]);
    if (i % 2 === 1) {
      n *= 2;
      if (n > 9) n -= 9;
    }
    total += n;
  }
  return total % 10 === 0;
}

const EMAIL_PATTERN = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;
const POSTAL_CODE_PATTERN = /^[A-Za-z]\d[A-Za-z] ?\d[A-Za-z]\d$/;
const ISO_DATE_PREFIX = /^(\d{4})-(\d{2})-(\d{2})(?:$|T)/;

function isRealDate(year: number, month: number, day: number): boolean {
  if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1) {
    return false;
  }
  const daysInMonth = new Date(Date.UTC(year, month, 0)).getUTCDate();
  return day <= daysInMonth;
}

const sin: ValidationRule = (value) => {
  // Masking is presentation: "046 454 286" pasted from a document is accepted,
  // and the mask characters never reach the checksum.
  const digits = digitsOf(value);
  if (digits.length !== 9) return failure("sin", RULE_KEYWORDS.sinWrongLength);
  // All zeros satisfies the arithmetic but is not a SIN.
  if (digits === "000000000" || !passesLuhn(digits)) {
    return failure("sin", RULE_KEYWORDS.sinInvalidChecksum);
  }
  return null;
};

const email: ValidationRule = (value) =>
  EMAIL_PATTERN.test(value.trim())
    ? null
    : failure("email", RULE_KEYWORDS.emailInvalidFormat);

const phone: ValidationRule = (value) => {
  let digits = digitsOf(value);
  if (digits.length === 11 && digits.startsWith("1")) digits = digits.slice(1);
  return digits.length === 10
    ? null
    : failure("phone", RULE_KEYWORDS.phoneInvalidFormat);
};

const postalCode: ValidationRule = (value) =>
  POSTAL_CODE_PATTERN.test(value.trim())
    ? null
    : failure("postalCode", RULE_KEYWORDS.postalCodeInvalidFormat);

/** An ISO date, or a datetime whose date portion is checked. */
const date: ValidationRule = (value) => {
  const match = ISO_DATE_PREFIX.exec(value.trim());
  if (
    !match ||
    !isRealDate(Number(match[1]), Number(match[2]), Number(match[3]))
  ) {
    return failure("date", RULE_KEYWORDS.dateInvalid);
  }
  return null;
};

/** A date part as typed: up to four ASCII digits, the same as the server reads. */
function datePart(text: string): number | null {
  const trimmed = text.trim();
  return /^[0-9]{1,4}$/.test(trimmed) ? Number(trimmed) : null;
}

function partAsInt(data: Record<string, unknown>, key: unknown): number | null {
  if (typeof key !== "string" || key === "") return null;
  const raw = data[key];
  return datePart(typeof raw === "number" ? String(raw) : textOrEmpty(raw));
}

function textOrEmpty(value: unknown): string {
  return typeof value === "string" ? value : "";
}

/**
 * The day of a day/month/year group. The component names its siblings in
 * `properties.myssDateParts: { month, year }`; a part that is missing or not
 * a number is the required or pattern rule's business, so only a complete
 * numeric group is checked, and it must make a real date ("100" is a bad day,
 * not a non-number to ignore).
 */
const dateParts: ValidationRule = (value, { data, component }) => {
  const parts = (component.properties as Record<string, unknown> | undefined)
    ?.myssDateParts as Record<string, unknown> | undefined;
  const day = datePart(value);
  const month = partAsInt(data, parts?.month);
  const year = partAsInt(data, parts?.year);
  if (day === null || month === null || year === null) return null;
  return isRealDate(year, month, day)
    ? null
    : failure("dateParts", RULE_KEYWORDS.dateInvalid);
};

/** Every rule a field can name in `properties.myssValidator`. */
export const VALIDATION_RULES: Readonly<Record<string, ValidationRule>> = {
  sin,
  email,
  phone,
  postalCode,
  date,
  dateParts,
};

/** Runs the named rule; an unknown rule name is not a failure. */
export function checkRule(
  rule: string,
  value: string,
  context: RuleContext,
): RuleFailure | null {
  const validator = VALIDATION_RULES[rule];
  return validator ? validator(value, context) : null;
}

/**
 * A confirmation field against the field it confirms, compared trimmed and
 * case-insensitively: mail domains are case-insensitive, and a citizen
 * retyping their address with different capitalisation has not made a
 * mistake worth blocking them over.
 */
export function checkMatches(
  value: string,
  confirmed: unknown,
): RuleFailure | null {
  const other = typeof confirmed === "string" ? confirmed : "";
  return value.trim().toLowerCase() === other.trim().toLowerCase()
    ? null
    : failure("matches", RULE_KEYWORDS.emailMismatch);
}

/**
 * The wording for a failure, in the same order the API resolves it: the
 * form's own wording for this rule on this field (`errors.<rule>`), then the
 * field's catch-all `validate.customMessage`, then the catalogue's row for the
 * keyword, then the compiled text.
 */
export function ruleMessage(
  component: Record<string, unknown>,
  failed: RuleFailure,
): string {
  const errors = component.errors as Record<string, unknown> | undefined;
  const authored = errors?.[failed.rule];
  if (typeof authored === "string" && authored.trim() !== "") return authored;

  const validate = component.validate as Record<string, unknown> | undefined;
  const custom = validate?.customMessage;
  if (typeof custom === "string" && custom.trim() !== "") return custom;

  return catalogueMessage(failed.keyword) ?? failed.message;
}
