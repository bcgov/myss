// The citizen-facing text for every stable error keyword this system raises,
// seeded into the `error-message` collection on boot (create-if-missing, see
// index.ts). The live catalogue is what a Service Designer edits in the admin
// panel; this file only gives a fresh database its starting wording.
//
// These rows MUST stay identical to Shared/validation/error-messages.json, the
// contract this seed shares with MyssApi's compiled fallback
// (Apps/MyssApi/Data/ErrorMessageDefaults.cs). error-message-seed.test.ts reads
// that file and fails on any divergence, so edit the shared file first and
// mirror the change here. The rules for what belongs in the catalogue are in
// the shared file's header.

/** One seeded row: the keyword, its wording, and where it is raised. */
export interface ErrorMessageSeed {
  readonly keyword: string;
  readonly message: string;
  /** For the designer: where the keyword is raised. Never shown to a citizen. */
  readonly note?: string;
}

/**
 * DOMAIN.CONTEXT.NAME: upper-case segments joined by dots, at least two. The
 * same pattern the content type's `keyword` attribute enforces.
 */
export const ERROR_MESSAGE_KEYWORD_PATTERN = /^[A-Z0-9_]+(\.[A-Z0-9_]+)+$/;

/** Every row the bootstrap hook seeds, in the shared file's order. */
export const seededErrorMessages: readonly ErrorMessageSeed[] = [
  {
    keyword: "IDA.SIN.WRONG_LENGTH",
    message: "A Social Insurance Number must be 9 digits.",
    note: "A SIN field (properties.myssValidator = sin) with other than nine digits once formatting is stripped.",
  },
  {
    keyword: "IDA.SIN.INVALID_CHECKSUM",
    message: "That Social Insurance Number is not valid. Check the digits and try again.",
    note: "A nine-digit SIN that fails the Luhn check.",
  },
  {
    keyword: "IDA.EMAIL.INVALID_FORMAT",
    message: "Enter an email address in the format name@example.com.",
    note: "An email field whose value is not a recognisable address.",
  },
  {
    keyword: "IDA.EMAIL.MISMATCH",
    message: "The two email addresses do not match.",
    note: "A confirmation field (properties.myssMatches) that differs from the field it confirms.",
  },
  {
    keyword: "IDA.PHONE.INVALID_FORMAT",
    message: "Enter a 10-digit phone number, for example 250 555 0199.",
    note: "A phone field (properties.myssValidator = phone) with other than ten digits once formatting is stripped.",
  },
  {
    keyword: "IDA.POSTAL_CODE.INVALID_FORMAT",
    message: "Enter a postal code in the format A1A 1A1.",
    note: "A postal code field (properties.myssValidator = postalCode) that is not a Canadian postal code.",
  },
  {
    keyword: "FORM.DATE.INVALID",
    message: "Enter a valid date.",
    note: "A date field (properties.myssValidator = date), or the day of a day/month/year group (dateParts), that is not a real date.",
  },
  {
    keyword: "FORM.FIELD.PATTERN",
    message: "This answer is not in the expected format.",
    note: "A field whose validate.pattern the answer does not match. Forms usually word this per field with errors.pattern.",
  },
  {
    keyword: "FORM.FIELD.MIN_LENGTH",
    message: "This answer is too short.",
    note: "A field whose validate.minLength the answer is under.",
  },
  {
    keyword: "FORM.FIELD.MAX_LENGTH",
    message: "This answer is too long.",
    note: "A field whose validate.maxLength the answer is over.",
  },
  {
    keyword: "FORM.FIELD.MIN",
    message: "This number is too small.",
    note: "A number under the field's validate.min.",
  },
  {
    keyword: "FORM.FIELD.MAX",
    message: "This number is too large.",
    note: "A number over the field's validate.max.",
  },
  {
    keyword: "REGISTRATION.DOB.INVALID",
    message: "Enter a valid date of birth.",
    note: "Registration: the date of birth is not a real date.",
  },
  {
    keyword: "REGISTRATION.DOB.FUTURE",
    message: "Date of birth cannot be in the future.",
    note: "Registration: the date of birth is after today.",
  },
  {
    keyword: "BUSPASS.REQUEST.TYPE_INVALID",
    message: "Select a valid service type",
    note: "Bus pass: the service type selector holds a value the form does not offer.",
  },
  {
    keyword: "BUSPASS.IDENTITY.IDENTIFIER_REQUIRED",
    message: "A Social Insurance Number or bus pass account number is required",
    note: "Bus pass: neither a SIN nor a bus pass account number was given.",
  },
  {
    keyword: "BUSPASS.DOB.INVALID",
    message: "Enter a valid date of birth",
    note: "Bus pass: the day, month and year do not make a real date.",
  },
  {
    keyword: "BUSPASS.DOB.FUTURE",
    message: "The date of birth cannot be in the future",
    note: "Bus pass: the date of birth is after today.",
  },
  {
    keyword: "BUSPASS.DOB.UNDER_16",
    message: "You must be at least 16",
    note: "Bus pass: the applicant is younger than the program's minimum age of 16.",
  },
  {
    keyword: "BUSPASS.CONTACT.EMAIL_REQUIRED",
    message: "An email address is required when email is the preferred means of communication",
    note: "Bus pass: email was chosen as the contact method but no address was given.",
  },
  {
    keyword: "BUSPASS.CONTACT.EMAIL_MISMATCH",
    message: "The two email addresses do not match",
    note: "Bus pass: the email and its verification field differ.",
  },
  {
    keyword: "BUSPASS.SUBMIT.REJECTED",
    message:
      "The BC Bus Pass Program could not accept this request. Check that the details you entered match what the ministry has on file, or contact the program for help.",
    note: "Bus pass: the ministry received the request and declined it. Worded by the browser; the API returns the keyword.",
  },
  {
    keyword: "BUSPASS.SUBMIT.RATE_LIMITED",
    message:
      "Too many requests have been sent from your connection in a short time. Wait a few minutes and try again.",
    note: "Bus pass: too many submissions from one connection in the window. Worded by the browser; the API returns the keyword.",
  },
];
