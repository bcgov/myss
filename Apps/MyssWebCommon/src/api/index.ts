// API shapes and helpers both webclients share. Each app keeps its own calls;
// these are the payloads and refusals the two APIs answer with alike.

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
