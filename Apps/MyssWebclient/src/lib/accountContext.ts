// Who a registration makes the account for, and the checks on that context's
// own answers. Mirrors the API's AccountCreationRules: the same fields,
// limits, keywords and wording, so the browser refuses what the API would.
// The API re-checks everything, and alone checks that each document is the
// caller's.

import { formatFileSize } from "@/api/attachments";
import type { FormValidationError } from "@/api/forms";
import { paths } from "@/routes/paths";
import { catalogueMessage } from "@myss/formio/errorCatalogue";
import { formatPhoneInput, phoneDigits } from "@myss/formio/phone";

export type AccountContextValue =
  "self" | "withHelper" | "legalRepresentative" | "parentForMinor";

export interface AccountContext {
  /** What the API reads. */
  value: AccountContextValue;
  /** What the address carries through sign-in. */
  slug: string;
  label: string;
}

/** The options, in the order the questions page offers them. */
export const ACCOUNT_CONTEXTS: readonly AccountContext[] = [
  {
    value: "self",
    slug: "self",
    label: "I’m making an account for myself (and family, if applicable)",
  },
  {
    value: "withHelper",
    slug: "with-helper",
    label: "I’m making an account for myself with help from someone else",
  },
  {
    value: "legalRepresentative",
    slug: "legal-representative",
    label:
      "I’m a legal representative who is creating an account on behalf of someone else",
  },
  {
    value: "parentForMinor",
    slug: "parent-for-minor",
    label: "I’m a parent making an account for a minor child",
  },
];

export const HELPER_QUESTION =
  "Do you authorize us to share your information with this person?";

export const HELPER_ANSWERS = [
  { value: "yes", label: "Yes" },
  {
    value: "no",
    label:
      "No, I would prefer the ministry connect only with me (or my spouse, if applicable)",
  },
] as const;

/** The info bar in the representative contexts, before sign-in and again in the form. */
export const REPRESENTATIVE_INFO =
  "All remaining questions relate to the person you are representing.";

/** The signed Consent to Disclosure of Information form (PDF). */
export const HR3189_URL =
  "https://www2.gov.bc.ca/assets/gov/british-columbians-our-governments/policies-for-government/bc-employment-assistance-policy-procedure-manual/forms/pdfs/HR3189.pdf";

/** The longest name or agency, after trimming. */
export const MAX_TEXT_LENGTH = 200;

/** The most documents of each kind a registration may attach. */
export const MAX_DOCUMENTS = 5;

/** The request's field names, which the API reports errors by. */
export const ACCOUNT_CREATION_FIELDS = {
  context: "accountContext",
  helperAuthorized: "helperAuthorized",
  consentDocuments: "consentDocuments",
  powerOfAttorneyDocuments: "powerOfAttorneyDocuments",
  representativeFirstName: "representativeFirstName",
  representativeLastName: "representativeLastName",
  representativePhone: "representativePhone",
  representativeAgency: "representativeAgency",
} as const;

export type DocumentField =
  | typeof ACCOUNT_CREATION_FIELDS.consentDocuments
  | typeof ACCOUNT_CREATION_FIELDS.powerOfAttorneyDocuments;

export const ACCOUNT_CREATION_KEYWORDS = {
  contextRequired: "REGISTRATION.CONTEXT.REQUIRED",
  contextUnknown: "REGISTRATION.CONTEXT.UNKNOWN",
  signInNotAllowed: "REGISTRATION.CONTEXT.SIGN_IN_NOT_ALLOWED",
  helperAuthorizationRequired: "REGISTRATION.HELPER.AUTHORIZATION_REQUIRED",
  consentRequired: "REGISTRATION.DOCUMENT.CONSENT_REQUIRED",
  tooManyDocuments: "REGISTRATION.DOCUMENT.TOO_MANY",
  documentNotFound: "REGISTRATION.DOCUMENT.NOT_FOUND",
  fieldRequired: "FORM.FIELD.REQUIRED",
  fieldMaxLength: "FORM.FIELD.MAX_LENGTH",
  phoneInvalid: "IDA.PHONE.INVALID_FORMAT",
} as const;

const K = ACCOUNT_CREATION_KEYWORDS;

/**
 * The compiled wording. MUST stay identical to
 * Shared/validation/error-messages.json, which the unit test checks; the
 * catalogue the Service Designer publishes replaces it at run time.
 */
export const ACCOUNT_CREATION_DEFAULT_MESSAGES: Readonly<
  Record<string, string>
> = {
  [K.contextRequired]: "Select the option that describes you.",
  [K.contextUnknown]: "Select one of the options shown.",
  [K.signInNotAllowed]:
    "To create an account for someone else, sign in with Basic BCeID.",
  [K.helperAuthorizationRequired]:
    "Select whether we may share your information with this person.",
  [K.consentRequired]:
    "Attach your signed Consent to Disclosure of Information form.",
  [K.tooManyDocuments]: "Attach no more than 5 documents.",
  [K.documentNotFound]:
    "One of the attached documents could not be found. Remove it and attach it again.",
  [K.fieldMaxLength]: "This answer is too long.",
  [K.phoneInvalid]: "Phone number is invalid",
};

/** The wording a citizen reads for a keyword: the catalogue's, else ours. */
export function accountCreationMessage(keyword: string): string {
  return (
    catalogueMessage(keyword) ??
    ACCOUNT_CREATION_DEFAULT_MESSAGES[keyword] ??
    keyword
  );
}

/** A file the answers list, kept in the tab before sign-in or uploaded after. */
export interface AccountDocument {
  /** The kept file's ID in the tab's store, or the attachment's ID once uploaded. */
  id: string;
  fileName: string;
  sizeBytes: number;
  state: "kept" | "attached";
}

export interface AccountCreationAnswers {
  context: AccountContextValue | null;
  helperAuthorized: "yes" | "no" | null;
  representativeFirstName: string;
  representativeLastName: string;
  representativePhone: string;
  representativeAgency: string;
  consentDocuments: AccountDocument[];
  powerOfAttorneyDocuments: AccountDocument[];
}

/** What the registration request carries beside the answers. */
export interface AccountCreationRequest {
  context: AccountContextValue;
  helperAuthorized?: boolean;
  representative?: {
    firstName: string;
    lastName: string;
    phone: string;
    agency?: string;
  };
  consentDocumentIds?: string[];
  powerOfAttorneyDocumentIds?: string[];
}

/** The answers the questions open with: Self, and No once the helper is asked about. */
export function openingAnswers(): AccountCreationAnswers {
  return {
    context: "self",
    helperAuthorized: "no",
    representativeFirstName: "",
    representativeLastName: "",
    representativePhone: "",
    representativeAgency: "",
    consentDocuments: [],
    powerOfAttorneyDocuments: [],
  };
}

/** The context an address names, or null for anything unknown. */
export function contextFromSlug(
  slug: string | null | undefined,
): AccountContext | null {
  return ACCOUNT_CONTEXTS.find((c) => c.slug === slug) ?? null;
}

export function contextFromValue(
  value: AccountContextValue | null | undefined,
): AccountContext | null {
  return ACCOUNT_CONTEXTS.find((c) => c.value === value) ?? null;
}

/** Someone else makes the account for the person it is for. */
export function isRepresentative(value: AccountContextValue): boolean {
  return value === "legalRepresentative" || value === "parentForMinor";
}

export function allowsBcServicesCard(value: AccountContextValue): boolean {
  return !isRepresentative(value);
}

export function asksForAgency(value: AccountContextValue): boolean {
  return value === "legalRepresentative";
}

export function asksForPowerOfAttorney(value: AccountContextValue): boolean {
  return value === "legalRepresentative";
}

/** The contexts a sign-in can register: all four with Basic BCeID, the first two otherwise. */
export function contextsFor(
  signedInWithBceid: boolean,
): readonly AccountContext[] {
  return signedInWithBceid
    ? ACCOUNT_CONTEXTS
    : ACCOUNT_CONTEXTS.filter((c) => allowsBcServicesCard(c.value));
}

/** The address the sign-in returns to, carrying the context. */
export function registrationPathFor(context: AccountContext): string {
  return `${paths.register}?context=${context.slug}`;
}

/** The document fields the answers show for their context. */
export function documentFieldsFor(
  answers: AccountCreationAnswers,
): DocumentField[] {
  const fields: DocumentField[] = [];
  if (answers.context === "withHelper" && answers.helperAuthorized === "yes") {
    fields.push(ACCOUNT_CREATION_FIELDS.consentDocuments);
  }
  if (answers.context && asksForPowerOfAttorney(answers.context)) {
    fields.push(ACCOUNT_CREATION_FIELDS.powerOfAttorneyDocuments);
  }
  return fields;
}

const DOCUMENT_FIELDS: readonly DocumentField[] = [
  ACCOUNT_CREATION_FIELDS.consentDocuments,
  ACCOUNT_CREATION_FIELDS.powerOfAttorneyDocuments,
];

/**
 * The answers without the documents of questions the context no longer asks,
 * such as a consent form attached before the authorization changed to No,
 * and those documents.
 */
export function withoutHiddenDocuments(answers: AccountCreationAnswers): {
  answers: AccountCreationAnswers;
  hidden: AccountDocument[];
} {
  const shown = new Set(documentFieldsFor(answers));
  const next = { ...answers };
  const hidden: AccountDocument[] = [];
  for (const field of DOCUMENT_FIELDS) {
    if (!shown.has(field)) {
      hidden.push(...answers[field]);
      next[field] = [];
    }
  }
  return { answers: next, hidden };
}

function fieldError(field: string, keyword: string, message?: string) {
  return {
    field,
    keyword,
    message: message ?? accountCreationMessage(keyword),
  };
}

function checkName(
  value: string,
  field: string,
  requiredMessage: string,
  errors: FormValidationError[],
) {
  const name = value.trim();
  if (name === "") {
    errors.push(fieldError(field, K.fieldRequired, requiredMessage));
  } else if (name.length > MAX_TEXT_LENGTH) {
    errors.push(fieldError(field, K.fieldMaxLength));
  }
}

function checkRepresentative(
  answers: AccountCreationAnswers,
  context: AccountContextValue,
  errors: FormValidationError[],
) {
  const F = ACCOUNT_CREATION_FIELDS;
  checkName(
    answers.representativeFirstName,
    F.representativeFirstName,
    "Enter your first name.",
    errors,
  );
  checkName(
    answers.representativeLastName,
    F.representativeLastName,
    "Enter your last name.",
    errors,
  );

  const phone = answers.representativePhone.trim();
  if (phone === "") {
    errors.push(
      fieldError(
        F.representativePhone,
        K.fieldRequired,
        "Enter your phone number.",
      ),
    );
  } else if (phoneDigits(phone) === null) {
    errors.push(fieldError(F.representativePhone, K.phoneInvalid));
  }

  if (
    asksForAgency(context) &&
    answers.representativeAgency.trim().length > MAX_TEXT_LENGTH
  ) {
    errors.push(fieldError(F.representativeAgency, K.fieldMaxLength));
  }
}

function checkDocumentCount(
  documents: AccountDocument[],
  field: string,
  errors: FormValidationError[],
) {
  if (documents.length > MAX_DOCUMENTS) {
    errors.push(fieldError(field, K.tooManyDocuments));
  }
}

function checkHelper(
  answers: AccountCreationAnswers,
  canAttach: boolean,
  errors: FormValidationError[],
) {
  const F = ACCOUNT_CREATION_FIELDS;
  if (answers.helperAuthorized === null) {
    errors.push(fieldError(F.helperAuthorized, K.helperAuthorizationRequired));
    return;
  }
  if (answers.helperAuthorized === "no") return;
  // While the browser cannot keep files, the form is attached after sign-in.
  if (answers.consentDocuments.length === 0) {
    if (canAttach)
      errors.push(fieldError(F.consentDocuments, K.consentRequired));
    return;
  }
  checkDocumentCount(answers.consentDocuments, F.consentDocuments, errors);
}

/**
 * The checks the browser can make, in page order: a context is chosen, and
 * that context's own answers are complete. Which contexts a sign-in allows is
 * enforced by the options offered.
 */
export function checkAccountCreation(
  answers: AccountCreationAnswers,
  { canAttach }: { canAttach: boolean },
): FormValidationError[] {
  const errors: FormValidationError[] = [];
  const context = answers.context;
  if (context === null) {
    return [fieldError(ACCOUNT_CREATION_FIELDS.context, K.contextRequired)];
  }
  if (context === "withHelper") {
    checkHelper(answers, canAttach, errors);
  }
  if (isRepresentative(context)) {
    checkRepresentative(answers, context, errors);
  }
  if (asksForPowerOfAttorney(context)) {
    checkDocumentCount(
      answers.powerOfAttorneyDocuments,
      ACCOUNT_CREATION_FIELDS.powerOfAttorneyDocuments,
      errors,
    );
  }
  return errors;
}

function attachedIds(documents: AccountDocument[]): string[] {
  return documents.filter((d) => d.state === "attached").map((d) => d.id);
}

/** The request's account-creation data: the chosen context's answers only. */
export function toAccountCreationRequest(
  answers: AccountCreationAnswers & { context: AccountContextValue },
): AccountCreationRequest {
  const { context } = answers;
  const request: AccountCreationRequest = { context };
  if (context === "withHelper") {
    request.helperAuthorized = answers.helperAuthorized === "yes";
    if (request.helperAuthorized) {
      request.consentDocumentIds = attachedIds(answers.consentDocuments);
    }
  }
  if (isRepresentative(context)) {
    const agency = answers.representativeAgency.trim();
    request.representative = {
      firstName: answers.representativeFirstName.trim(),
      lastName: answers.representativeLastName.trim(),
      phone: answers.representativePhone.trim(),
      ...(asksForAgency(context) && agency !== "" ? { agency } : {}),
    };
  }
  if (asksForPowerOfAttorney(context)) {
    request.powerOfAttorneyDocumentIds = attachedIds(
      answers.powerOfAttorneyDocuments,
    );
  }
  return request;
}

const FIELD_NAMES: ReadonlySet<string> = new Set(
  Object.values(ACCOUNT_CREATION_FIELDS),
);

/** Whether an error from the API is about the account-creation answers. */
export function isAccountCreationField(field: string): boolean {
  return FIELD_NAMES.has(field);
}

/** One answer as the review lists it; a list for documents. */
export interface RecapRow {
  label: string;
  value: string | string[];
}

function documentList(documents: AccountDocument[]): string | string[] {
  if (documents.length === 0) return "None attached";
  return documents.map((d) => `${d.fileName} (${formatFileSize(d.sizeBytes)})`);
}

function representativeRows(
  answers: AccountCreationAnswers,
  context: AccountContextValue,
): RecapRow[] {
  const phone = answers.representativePhone.trim();
  const rows: RecapRow[] = [
    {
      label: "Your name (authorized representative)",
      value: `${answers.representativeFirstName.trim()} ${answers.representativeLastName.trim()}`,
    },
    {
      label: "Your phone number",
      value: formatPhoneInput(phoneDigits(phone) ?? phone),
    },
  ];
  if (asksForAgency(context)) {
    rows.push({
      label: "Agency",
      value: answers.representativeAgency.trim() || "Not provided",
    });
  }
  if (asksForPowerOfAttorney(context)) {
    rows.push({
      label: "Power of attorney documents",
      value: documentList(answers.powerOfAttorneyDocuments),
    });
  }
  return rows;
}

/** The chosen context's answers as the registration's review lists them. */
export function accountCreationRecapRows(
  answers: AccountCreationAnswers,
): RecapRow[] {
  const context = contextFromValue(answers.context);
  if (!context) return [];
  const rows: RecapRow[] = [
    { label: "Who the account is for", value: context.label },
  ];
  if (context.value === "withHelper") {
    const authorized = answers.helperAuthorized === "yes";
    rows.push({
      label: "May we share your information with this person?",
      value: authorized ? HELPER_ANSWERS[0].label : HELPER_ANSWERS[1].label,
    });
    if (authorized) {
      rows.push({
        label: "Consent to Disclosure of Information",
        value: documentList(answers.consentDocuments),
      });
    }
  }
  if (isRepresentative(context.value)) {
    rows.push(...representativeRows(answers, context.value));
  }
  return rows;
}
