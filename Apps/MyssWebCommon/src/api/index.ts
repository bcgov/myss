// API shapes and helpers both webclients share. Each app keeps its own calls;
// these are the payloads and refusals the two APIs answer with alike.

export {
  ACKNOWLEDGEMENT_FORM_IDS,
  ACKNOWLEDGEMENT_KEYWORDS,
  ACKNOWLEDGEMENT_LIMITS,
  BUS_PASS_ACKNOWLEDGEMENT,
  isSafeLinkTarget,
  normaliseAcknowledgementText,
  REFERENCE_PLACEHOLDER,
  validateAcknowledgement,
  type AcknowledgementError,
  type AcknowledgementField,
  type SubmissionAcknowledgement,
} from "./acknowledgements";
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
