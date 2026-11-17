// Form messages, the texts a form's page shows applicants: their shape, the built-in
// texts and the rules, which match the admin API's FormMessageValidator.

/** The three texts of a form message, as both APIs serve them. */
export interface FormMessageTexts {
  heading: string;
  title: string;
  body: string;
}

export type FormMessageField = keyof FormMessageTexts;

/** A rule a form message broke, on the field it concerns. */
export interface FormMessageError {
  field: FormMessageField;
  keyword: string;
  message: string;
}

/** A `{name}` placeholder a message may use, as the message's definition lists it. */
export interface FormMessagePlaceholder {
  name: string;
  description: string;
  sample: string;
  required: boolean;
}

/** The most characters each text may hold. */
export const FORM_MESSAGE_LIMITS: Readonly<Record<FormMessageField, number>> = {
  heading: 100,
  title: 100,
  body: 1000,
};

export const FORM_MESSAGE_KEYWORDS = {
  textRequired: "FORM_MESSAGE.TEXT.REQUIRED",
  textTooLong: "FORM_MESSAGE.TEXT.TOO_LONG",
  placeholderMissing: "FORM_MESSAGE.BODY.PLACEHOLDER_MISSING",
  placeholderUnknown: "FORM_MESSAGE.BODY.PLACEHOLDER_UNKNOWN",
  linkInvalid: "FORM_MESSAGE.BODY.LINK_INVALID",
  changed: "FORM_MESSAGE.CHANGED",
} as const;

/** What the bus pass page shows after a submission when no message is saved or it cannot be read. */
export const BUS_PASS_CONFIRMATION: Readonly<FormMessageTexts> = {
  heading: "Bus Pass Request is complete",
  title: "Request submitted",
  body:
    "The BC Bus Pass Program has received your request. Your reference number is {referenceNumber}. Keep it for your records.\n" +
    "We may contact you if we need further information for your request.",
};

const BUILT_IN = new Map<string, Readonly<FormMessageTexts>>([
  ["bc-bus-pass/confirmation", BUS_PASS_CONFIRMATION],
]);

/** A form message's built-in texts, or undefined when it has none. */
export function builtInFormMessage(
  formSpecId: string,
  messageKey: string,
): Readonly<FormMessageTexts> | undefined {
  return BUILT_IN.get(`${formSpecId}/${messageKey}`);
}

const FIELDS: readonly {
  field: FormMessageField;
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
export function normaliseFormMessageText(text: string): string {
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
): FormMessageError[] {
  if (text.length === 0) {
    return [
      {
        field: field.field,
        keyword: FORM_MESSAGE_KEYWORDS.textRequired,
        message: field.required,
      },
    ];
  }
  const limit = FORM_MESSAGE_LIMITS[field.field];
  if (text.length > limit) {
    return [
      {
        field: field.field,
        keyword: FORM_MESSAGE_KEYWORDS.textTooLong,
        message: `${field.label} must be ${count(limit)} characters or fewer. It has ${count(text.length)}.`,
      },
    ];
  }
  return [];
}

function allowedSentence(allowed: readonly string[]): string {
  if (allowed.length === 0) return "";
  if (allowed.length === 1) return ` The only one is ${allowed[0]}.`;
  const last = allowed[allowed.length - 1];
  return ` The ones it can use are ${allowed.slice(0, -1).join(", ")} and ${last}.`;
}

function placeholderErrors(
  body: string,
  placeholders: readonly FormMessagePlaceholder[],
): FormMessageError[] {
  const errors: FormMessageError[] = [];
  // A placeholder inside a link is not replaced, so it does not count.
  const outsideLinks = body.replace(LINK_PATTERN, "");
  for (const placeholder of placeholders) {
    const braced = `{${placeholder.name}}`;
    if (placeholder.required && !outsideLinks.includes(braced)) {
      errors.push({
        field: "body",
        keyword: FORM_MESSAGE_KEYWORDS.placeholderMissing,
        message: `Add ${braced} where ${placeholder.description} should appear.`,
      });
    }
  }
  const allowed = placeholders.map((placeholder) => `{${placeholder.name}}`);
  const unknown = new Set(
    Array.from(body.matchAll(PLACEHOLDER_PATTERN), (match) => match[0]).filter(
      (found) => !allowed.includes(found),
    ),
  );
  for (const found of unknown) {
    errors.push({
      field: "body",
      keyword: FORM_MESSAGE_KEYWORDS.placeholderUnknown,
      message: `${found} is not a placeholder this message can use.${allowedSentence(allowed)}`,
    });
  }
  return errors;
}

function linkErrors(body: string): FormMessageError[] {
  return Array.from(body.matchAll(LINK_PATTERN))
    .filter(([, , target]) => !isSafeLinkTarget(target))
    .map(([, text]) => ({
      field: "body",
      keyword: FORM_MESSAGE_KEYWORDS.linkInvalid,
      message: `The link “${text}” must start with https://, mailto: or tel:, with no spaces.`,
    }));
}

/**
 * Every rule a form message breaks, field by field; empty when it can be saved.
 * The placeholders are the ones the message's definition lists. Lengths are
 * counted after normaliseFormMessageText; a message over its limit is not
 * checked further.
 */
export function validateFormMessage(
  texts: FormMessageTexts,
  placeholders: readonly FormMessagePlaceholder[],
): FormMessageError[] {
  const body = normaliseFormMessageText(texts.body);
  const errors = FIELDS.flatMap((field) =>
    textErrors(field, normaliseFormMessageText(texts[field.field])),
  );
  const checkContent = body.length > 0 && body.length <= FORM_MESSAGE_LIMITS.body;
  return checkContent
    ? [...errors, ...placeholderErrors(body, placeholders), ...linkErrors(body)]
    : errors;
}
