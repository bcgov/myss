// The confirmation message shown after a successful submission: its shape, the built-in
// texts and its rules, which match the admin API's SubmissionAcknowledgementValidator.

/** The three texts of a confirmation message, as both APIs serve them. */
export interface SubmissionAcknowledgement {
  heading: string;
  title: string;
  body: string;
}

export type AcknowledgementField = keyof SubmissionAcknowledgement;

/** A rule a confirmation message broke, on the field it concerns. */
export interface AcknowledgementError {
  field: AcknowledgementField;
  keyword: string;
  message: string;
}

/** The forms whose page shows a confirmation message read from the content engine. */
export const ACKNOWLEDGEMENT_FORM_IDS: readonly string[] = ["bc-bus-pass"];

/** Replaced on the page with the applicant's reference number. */
export const REFERENCE_PLACEHOLDER = "{referenceNumber}";

/** The most characters each text may hold. */
export const ACKNOWLEDGEMENT_LIMITS: Readonly<
  Record<AcknowledgementField, number>
> = { heading: 100, title: 100, body: 1000 };

export const ACKNOWLEDGEMENT_KEYWORDS = {
  textRequired: "ACKNOWLEDGEMENT.TEXT.REQUIRED",
  textTooLong: "ACKNOWLEDGEMENT.TEXT.TOO_LONG",
  referenceMissing: "ACKNOWLEDGEMENT.BODY.REFERENCE_MISSING",
  placeholderUnknown: "ACKNOWLEDGEMENT.BODY.PLACEHOLDER_UNKNOWN",
  linkInvalid: "ACKNOWLEDGEMENT.BODY.LINK_INVALID",
  changed: "ACKNOWLEDGEMENT.CHANGED",
} as const;

/** What the bus pass page shows when no message is saved or it cannot be read. */
export const BUS_PASS_ACKNOWLEDGEMENT: Readonly<SubmissionAcknowledgement> = {
  heading: "Bus Pass Request is complete",
  title: "Request submitted",
  body:
    "The BC Bus Pass Program has received your request. Your reference number is {referenceNumber}. Keep it for your records.\n" +
    "We may contact you if we need further information for your request.",
};

const FIELDS: readonly {
  field: AcknowledgementField;
  label: string;
  required: string;
}[] = [
  { field: "heading", label: "The page heading", required: "Enter a page heading." },
  { field: "title", label: "The message title", required: "Enter a message title." },
  { field: "body", label: "The message", required: "Enter the message." },
];

const PLACEHOLDER_PATTERN = /\{[^{}\n]+\}/g;
const LINK_PATTERN = /\[([^\]\n]+)\]\(([^)\n]*)\)/g;

/** The text as it is checked and stored: every line break a line feed, and no spaces at either end. */
export function normaliseAcknowledgementText(text: string): string {
  return text.replace(/\r\n?/g, "\n").trim();
}

/** Whether a link target is one the page renders as a link: https, email or phone. */
export function isSafeLinkTarget(target: string): boolean {
  return /^(https:\/\/|mailto:|tel:)\S+$/i.test(target);
}

function count(value: number): string {
  return value.toLocaleString("en-CA");
}

function textErrors(
  field: (typeof FIELDS)[number],
  text: string,
): AcknowledgementError[] {
  if (text.length === 0) {
    return [
      {
        field: field.field,
        keyword: ACKNOWLEDGEMENT_KEYWORDS.textRequired,
        message: field.required,
      },
    ];
  }
  const limit = ACKNOWLEDGEMENT_LIMITS[field.field];
  if (text.length > limit) {
    return [
      {
        field: field.field,
        keyword: ACKNOWLEDGEMENT_KEYWORDS.textTooLong,
        message: `${field.label} must be ${count(limit)} characters or fewer. It has ${count(text.length)}.`,
      },
    ];
  }
  return [];
}

function bodyErrors(body: string): AcknowledgementError[] {
  const errors: AcknowledgementError[] = [];
  // A placeholder inside a link is not replaced, so it does not count.
  if (!body.replace(LINK_PATTERN, "").includes(REFERENCE_PLACEHOLDER)) {
    errors.push({
      field: "body",
      keyword: ACKNOWLEDGEMENT_KEYWORDS.referenceMissing,
      message: `Add ${REFERENCE_PLACEHOLDER} where the applicant's reference number should appear.`,
    });
  }
  const unknown = new Set(
    Array.from(body.matchAll(PLACEHOLDER_PATTERN), (match) => match[0]).filter(
      (placeholder) => placeholder !== REFERENCE_PLACEHOLDER,
    ),
  );
  for (const placeholder of unknown) {
    errors.push({
      field: "body",
      keyword: ACKNOWLEDGEMENT_KEYWORDS.placeholderUnknown,
      message: `${placeholder} is not a placeholder this message can use. The only one is ${REFERENCE_PLACEHOLDER}.`,
    });
  }
  for (const [, text, target] of body.matchAll(LINK_PATTERN)) {
    if (!isSafeLinkTarget(target)) {
      errors.push({
        field: "body",
        keyword: ACKNOWLEDGEMENT_KEYWORDS.linkInvalid,
        message: `The link “${text}” must start with https://, mailto: or tel:, with no spaces.`,
      });
    }
  }
  return errors;
}

/**
 * Every rule a confirmation message breaks, field by field; empty when it can
 * be saved. Lengths are counted after normaliseAcknowledgementText; a message
 * over its limit is not checked further.
 */
export function validateAcknowledgement(
  texts: SubmissionAcknowledgement,
): AcknowledgementError[] {
  const body = normaliseAcknowledgementText(texts.body);
  const errors = FIELDS.flatMap((field) =>
    textErrors(field, normaliseAcknowledgementText(texts[field.field])),
  );
  const checkContent = body.length > 0 && body.length <= ACKNOWLEDGEMENT_LIMITS.body;
  return checkContent ? [...errors, ...bodyErrors(body)] : errors;
}
