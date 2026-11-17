// The public surface of @myss/formio: the BC Gov Form.io components the
// citizen webclient renders forms with and the admin webclient previews them
// with, plus the spec helpers the admin editor uses. Both apps import from
// here so a form looks and validates the same in the editor's preview as it
// does for citizens.

export {
  registerBcgovComponents,
  type RegisterBcgovComponentsOptions,
} from "./bcgovComponents";
export { ALLOWED_COMPONENT_TYPES, builderOptions } from "./builderOptions";
export { revealAllComponents } from "./specPreview";
export {
  flattenComponents,
  listEditableComponents,
  setLabel,
  setRequired,
  type EditableComponent,
} from "./specEdit";
export { findDuplicateKeys, findUnknownConditionalTargets } from "./specRules";
export { forEachComponent } from "./specWalk";
export {
  EXAMPLE_SPEC,
  checkSpec,
  formatSpecJson,
  locateJsonError,
  locateKey,
  parseSpecJson,
  type ParseResult,
  type SpecCheck,
  type SpecProblem,
  type TextPosition,
} from "./specImport";

// The email rule and its wording, for an address typed outside a form.
export {
  DEFAULT_MESSAGES as RULE_DEFAULT_MESSAGES,
  RULE_KEYWORDS,
  isEmailAddress,
} from "./validationRules";

// The phone number input, shared with the citizen app's own forms.
export { default as PhoneInput } from "./PhoneInput";

// A spec drawn read-only over submitted answers: the applicant's view of their
// application and the worker's review view.
export { default as ReadOnlySpecForm } from "./ReadOnlySpecForm";

// Also exported, as their own entry points so code that only needs them (and
// its node-environment unit tests) does not load Form.io:
//   @myss/formio/errorCatalogue  the catalogue the components word rule
//                                failures from; the host app fills it
//   @myss/formio/phone           phone number rules, shared with the citizen
//                                app so a number is checked the same way
//                                inside and outside Form.io
