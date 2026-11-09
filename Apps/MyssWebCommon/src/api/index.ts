// API shapes and helpers both webclients share. Each app keeps its own calls;
// these are the payloads and refusals the two APIs answer with alike.

export {
  BUS_PASS_CONFIRMATION,
  builtInFormMessage,
  FORM_MESSAGE_KEYWORDS,
  FORM_MESSAGE_LIMITS,
  isSafeLinkTarget,
  normaliseFormMessageText,
  validateFormMessage,
  type FormMessageError,
  type FormMessageField,
  type FormMessagePlaceholder,
  type FormMessageTexts,
} from "./formMessages";
export {
  ApplicationRequestError,
  readProblem,
  throwApplicationRequestError,
  type ApplicationStatus,
  type Problem,
} from "./applications";
export {
  readValidationErrors,
  SubmissionRejectedError,
  type FormSpecPayload,
  type FormValidationError,
} from "./forms";
export { toValidationErrors } from "./formioErrors";
export type {
  AssetColumn,
  EligibilityAssetLimits,
  EligibilityRateRow,
  EligibilityRates,
  IncomeColumn,
} from "./rates";
