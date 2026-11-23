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
    message:
      "That Social Insurance Number is not valid. Check the digits and try again.",
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
    message: "Phone number is invalid",
    note: "A phone field (phoneNumber type, properties.myssValidator = phone, or a legacy phone mask) that does not match the legacy MySS phone format after whitespace is removed.",
  },
  {
    keyword: "IDA.POSTAL_CODE.INVALID_FORMAT",
    message: "Enter a postal code in the format A1A 1A1.",
    note: "A postal code field (properties.myssValidator = postalCode) that is not a Canadian postal code.",
  },
  {
    keyword: "FORM.DATE.INVALID",
    message: "Enter a valid date.",
    note: "A date field (properties.myssValidator = date), or the day of a day/month/year group (dateParts), that is not a real date. In a date question (datetime), also shown once when the month, day and year are all wrong.",
  },
  {
    keyword: "FORM.DATE.MONTH_REQUIRED",
    message: "A birth month is required",
    note: "Browser only: a date question (datetime) with a day or year typed but no month chosen. Legacy MySS had no month message; worded to match its day and year ones. The API only ever receives whole dates.",
  },
  {
    keyword: "FORM.DATE.DAY_REQUIRED",
    message: "A birth day is required",
    note: "Browser only: a date question (datetime) with the day left blank. Legacy MySS wording.",
  },
  {
    keyword: "FORM.DATE.DAY_INVALID",
    message: "Invalid Day",
    note: "Browser only: a date question (datetime) whose day is not a number from 1 to 31. Legacy MySS wording.",
  },
  {
    keyword: "FORM.DATE.YEAR_REQUIRED",
    message: "A birth year is required",
    note: "Browser only: a date question (datetime) with the year left blank. Legacy MySS wording.",
  },
  {
    keyword: "FORM.DATE.YEAR_INVALID",
    message: "Invalid Year",
    note: "Browser only: a date question (datetime) whose year is not 4 digits or is before 1900 (future years are accepted). Legacy MySS wording.",
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
    keyword: "FORM.FIELD.ROW_COUNT_MISMATCH",
    message: "The number of entries does not match the number you entered.",
    note: "A repeat section (bcgovRepeatSection) whose rows do not number the answer to its count question (properties.myssRepeatCount). The form keeps them equal, and a missing or unusable count is reported on the count question itself, so this is raised only for a request the form did not send.",
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
    keyword: "REGISTRATION.GENDER.UNKNOWN",
    message: "Choose one of the listed options.",
    note: "Registration: the gender is not one of the options the form offered.",
  },
  {
    keyword: "REGISTRATION.EMAIL.NOT_VERIFIED",
    message: "Verify your email address before you complete your registration.",
    note: "Registration: the email address has not been verified by its emailed link (MYSS-295).",
  },
  {
    keyword: "REGISTRATION.CONTEXT.REQUIRED",
    message: "Select the option that describes you.",
    note: "Registration: the citizen did not say who the account is for.",
  },
  {
    keyword: "REGISTRATION.CONTEXT.UNKNOWN",
    message: "Select one of the options shown.",
    note: "Registration: who the account is for is not one of the four options offered.",
  },
  {
    keyword: "REGISTRATION.CONTEXT.SIGN_IN_NOT_ALLOWED",
    message: "To create an account for someone else, sign in with Basic BCeID.",
    note: "Registration: a legal representative or a parent registered from a sign-in other than Basic BCeID.",
  },
  {
    keyword: "REGISTRATION.HELPER.AUTHORIZATION_REQUIRED",
    message: "Select whether we may share your information with this person.",
    note: "Registration: a citizen registering with help did not say whether the ministry may share their information with the helper.",
  },
  {
    keyword: "REGISTRATION.DOCUMENT.CONSENT_REQUIRED",
    message: "Attach your signed Consent to Disclosure of Information form.",
    note: "Registration: a citizen who authorized their helper attached no signed consent form.",
  },
  {
    keyword: "REGISTRATION.DOCUMENT.TOO_MANY",
    message: "Attach no more than 5 documents.",
    note: "Registration: more than 5 documents of one kind were attached.",
  },
  {
    keyword: "REGISTRATION.DOCUMENT.NOT_FOUND",
    message: "One of the attached documents could not be found. Remove it and attach it again.",
    note: "Registration: an attached document is not the citizen's, has not passed its virus scan, or is already linked to an account.",
  },
  {
    keyword: "ACCOUNT.PHONE.TYPE_UNKNOWN",
    message: "Choose a phone type: home, cell, work or message.",
    note: "Account info: a phone number's type is not one of Home, Cell, Work or Message.",
  },
  {
    keyword: "ACCOUNT.PHONE.DUPLICATE_TYPE",
    message:
      "You can have one phone number of each type. Choose a different type.",
    note: "Account info: two phone numbers were given the same type.",
  },
  {
    keyword: "ACCOUNT.PHONE.TOO_MANY",
    message: "You can have up to 4 phone numbers, one of each type.",
    note: "Account info: more phone numbers were sent than there are phone types.",
  },
  {
    keyword: "IDA.PIN.INVALID_FORMAT",
    message: "Enter a 4-digit PIN using numbers only.",
    note: "A PIN (registration or Account Info) that is not exactly four numeric digits.",
  },
  {
    keyword: "IDA.PIN.MISMATCH",
    message: "The two PINs do not match.",
    note: "A PIN confirmation that differs from the PIN it confirms.",
  },
  {
    keyword: "ACCOUNT.PIN.INCORRECT",
    message: "The current PIN is not correct.",
    note: "Account info: the current PIN given to change it is not the stored one. Repeated, it locks Change PIN.",
  },
  {
    keyword: "ACCOUNT.PIN.NOT_AVAILABLE",
    message: "A PIN is only used when you sign in with Basic BCeID.",
    note: "A PIN sent for a sign-in that does not use one (BC Services Card). The webclient never shows a PIN to those users.",
  },
  {
    keyword: "ACCOUNT.EMAIL.ADDRESS_REJECTED",
    message:
      "We could not send an email to that address. Check it and try again.",
    note: "Account Info email change: the email service refused the address itself (MYSS-326). Shown on the email field.",
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
    message:
      "An email address is required when email is the preferred means of communication",
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
