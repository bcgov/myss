/**
 * Seed data for the POC form spec.
 *
 * Extracted from `src/index.ts` so it can be imported by tests without
 * booting Strapi. Nothing in this module touches Strapi — it is plain data
 * and plain types, which is the whole point: the bootstrap hook stays a thin
 * wrapper and the content it seeds is independently testable.
 *
 * Lives under `src/lib/` rather than `src/api/form-spec/` deliberately.
 * Strapi's loader scans `src/api/<name>/` for `content-types`, `controllers`,
 * `routes`, `services`, `policies` and `middlewares`; `src/lib/` is not
 * scanned at all, so shared pure modules cannot confuse it.
 */

import busPassFormSpec from "./bus-pass-form.json";

export type Json =
  | string
  | number
  | boolean
  | null
  | Json[]
  | { [key: string]: Json };

/** The logical identifier for the BC Bus Pass form. */
export const BUS_PASS_FORM_SPEC_ID = "bc-bus-pass";

/** The human-readable title for the BC Bus Pass form. */
export const BUS_PASS_FORM_SPEC_TITLE = "BC Bus Pass";

/** The logical identifier every seeded version shares. */
export const POC_FORM_SPEC_ID = "poc-test-form";

/** The human-readable title every seeded version shares. */
export const POC_FORM_SPEC_TITLE = "POC test form";

// POC test form. v1 is seeded so a fresh database has a working form;
// later versions are authored through the admin panel as new entries.
export const testFormSpecV1: Json = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "firstName",
      label: "First name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "lastName",
      label: "Last name",
      input: true,
      validate: { required: true },
    },
    {
      type: "select",
      key: "relationship",
      label: "Relationship status",
      input: true,
      widget: "choicesjs",
      data: {
        values: [
          { value: "single", label: "Single" },
          { value: "couple", label: "Married / in a relationship" },
        ],
      },
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "spouseName",
      label: "Spouse name",
      input: true,
      conditional: { show: true, when: "relationship", eq: "couple" },
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Monthly income ($)",
      input: true,
      validate: { required: true, min: 0 },
    },
    {
      type: "checkbox",
      key: "declaration",
      label: "I declare the information provided is true and complete",
      input: true,
      validate: { required: true },
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Submit",
      input: true,
    },
  ],
};

// v2 adds a "Contact email" field and rewords the income label. Seeded as a
// separate entry; v1 stays as-is so old submissions keep rendering with it.
export const testFormSpecV2: Json = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "firstName",
      label: "First name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "lastName",
      label: "Last name",
      input: true,
      validate: { required: true },
    },
    {
      type: "select",
      key: "relationship",
      label: "Relationship status",
      input: true,
      widget: "choicesjs",
      data: {
        values: [
          { value: "single", label: "Single" },
          { value: "couple", label: "Married / in a relationship" },
        ],
      },
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "spouseName",
      label: "Spouse name",
      input: true,
      conditional: { show: true, when: "relationship", eq: "couple" },
    },
    {
      type: "email",
      key: "contactEmail",
      label: "Contact email (new in v2)",
      input: true,
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Total monthly income ($) (reworded in v2)",
      input: true,
      validate: { required: true, min: 0 },
    },
    {
      type: "checkbox",
      key: "declaration",
      label: "I declare the information provided is true and complete",
      input: true,
      validate: { required: true },
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Submit",
      input: true,
    },
  ],
};

// v3 adds a SIN field. The point of this version is the validator marker
// rather than the field itself: `properties.myssValidator` is how an ordinary
// Form.io textfield declares that MyssApi's `FormSpecValidator` should run the
// SIN rule (nine digits, Luhn mod-10) over its answer. Form.io's `properties`
// is a free-form key-value map it ignores, so a validated field is authored as
// ordinary content — no custom component, no deployment. Phase 1's `sin`
// component type will be the second route to the same rule.
//
// The marker spelling is a contract with MyssApi/Services/FormSpecValidator.cs.
// A typo fails silently: the field renders and the answer goes unvalidated,
// which is why the test file pins it.
export const testFormSpecV3: Json = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "firstName",
      label: "First name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "lastName",
      label: "Last name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "sin",
      label: "Social insurance number (new in v3)",
      description: "Nine digits. Validated server-side on submit.",
      input: true,
      validate: { required: true },
      properties: { myssValidator: "sin" },
    },
    {
      type: "select",
      key: "relationship",
      label: "Relationship status",
      input: true,
      widget: "choicesjs",
      data: {
        values: [
          { value: "single", label: "Single" },
          { value: "couple", label: "Married / in a relationship" },
        ],
      },
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "spouseName",
      label: "Spouse name",
      input: true,
      conditional: { show: true, when: "relationship", eq: "couple" },
    },
    {
      type: "email",
      key: "contactEmail",
      label: "Contact email",
      input: true,
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Total monthly income ($)",
      input: true,
      validate: { required: true, min: 0 },
    },
    {
      type: "checkbox",
      key: "declaration",
      label: "I declare the information provided is true and complete",
      input: true,
      validate: { required: true },
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Submit",
      input: true,
    },
  ],
};

/** A seeded version: the spec JSON and the version number it publishes as. */
export interface SeededFormSpec {
  readonly version: number;
  readonly spec: Json;
}

/**
 * Every version the bootstrap hook seeds, in ascending version order.
 * Adding a version here is the only change needed to seed another one.
 */
export const seededFormSpecs: readonly SeededFormSpec[] = [
  { version: 1, spec: testFormSpecV1 },
  { version: 2, spec: testFormSpecV2 },
  { version: 3, spec: testFormSpecV3 },
];

// ---------------------------------------------------------------------------
// Eligibility Estimator form
// ---------------------------------------------------------------------------
//
// The Pre-Eligibility Estimator (prod: https://myselfserve.gov.bc.ca/EligibilityEstimator),
// converted from a hardcoded React form into content served from Strapi. Unlike
// the POC form this one is NOT persisted: MyssApi renders it, the citizen submits,
// and an eligibility amount is computed and shown — nothing is stored.
//
// Two things about this spec are deliberate and must not be "tidied":
//
//   1. The spouse fields (partnerPwd + the four partner financial fields) use an
//      ADVANCED conditional (`conditional.json`, JSON-logic) because they must
//      reveal for TWO relationship values — `married` OR `marriagelike`. Form.io's
//      simple `{ when, eq }` conditional matches only one value.
//
//   2. Those same spouse fields carry NO `validate.required`. MyssApi's
//      FormSpecValidator only recognises the SIMPLE `conditional.when` string when
//      deciding whether to exempt a field from the server-side required check; an
//      advanced-conditional field is treated as always-present. A single applicant
//      never sees the spouse fields, so marking them required would reject that
//      applicant server-side for leaving them blank. Client-side conditional
//      required is sufficient for a public, non-persisted estimator.
//
// The component keys are a contract with the frontend mapper
// (Apps/MyssWebclient src/api/eligibility.ts `mapAnswersToEstimate`) which turns
// these answers into an EligibilityRequest for MyssApi's calculator.

/** The logical identifier the estimator form is served under. */
export const ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID = "eligibility-estimator";

/** The human-readable title shown in the admin listing. */
export const ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE = "Eligibility Estimator";

/** Yes/No radio option set — value strings the mapper turns into booleans. */
const yesNoValues: Json = [
  { label: "Yes", value: "true" },
  { label: "No", value: "false" },
];

/** Reveal the spouse fields for a partnered relationship (married OR marriage-like). */
const partneredConditional: Json = {
  json: { in: [{ var: "relationshipStatus" }, ["married", "marriagelike"]] },
};

export const eligibilityEstimatorSpecV1: Json = {
  display: "form",
  components: [
    {
      type: "radio",
      key: "relationshipStatus",
      label: "What is your relationship status?",
      input: true,
      values: [
        { label: "Single and Never Married", value: "single" },
        { label: "Married", value: "married" },
        { label: "Marriage-Like Relationship", value: "marriagelike" },
        { label: "Divorced", value: "divorced" },
        { label: "Separated", value: "separated" },
        { label: "Widowed", value: "widowed" },
      ],
      validate: { required: true },
    },
    {
      type: "number",
      key: "dependentChildren",
      label: "How many dependent children under the age of 19 live with you?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "radio",
      key: "pwd",
      label:
        "Do you plan to apply for the Persons with Disabilities (PWD) designation?",
      input: true,
      values: yesNoValues,
      validate: { required: true },
    },
    {
      // Advanced-conditional, NOT server-required — see the header note.
      type: "radio",
      key: "partnerPwd",
      label:
        "Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?",
      input: true,
      values: yesNoValues,
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Your Monthly Income",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "vehicleValueMinusTransportation",
      label:
        "What is the value of your vehicle minus any amount owing that is used for day to day transportation needs",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "vehicleValue",
      label:
        "What is the value minus any amount owing of all your other vehicles?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "assetValue",
      label:
        "Your Combined Value of Other Assets (Property, Investments, Cash, or Savings)",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "partnerMonthlyIncome",
      label: "Spouse's Monthly Income",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "partnerVehicleValueMinusTransportation",
      label:
        "What is the value of your spouse's vehicle minus any amount owing that is used for day to day transportation needs",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "partnerVehicleValue",
      label:
        "What is the value minus any amount owing of all your spouse's other vehicles?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "partnerAssetValue",
      label:
        "Spouse's Combined Value of Other Assets (Property, Investments, Cash, or Savings)",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Get Estimate",
      input: true,
    },
  ],
};

// ---------------------------------------------------------------------------
// Estimator spec v2 — 2026-08 redesign (MYSS-169, Step 1 / Group B)
// ---------------------------------------------------------------------------
//
// v2 prepends the residency / citizenship PRE-CHECK radios and applies the 0826
// asset-field label rewrites. LABELS ONLY changed from v1 — every `key` is
// identical, because the keys are the contract with the frontend mapper
// (`mapAnswersToEstimate`) and MyssApi's FormSpecValidator. v1 stays seeded for
// idempotency; v2 becomes the latest published version.
//
// A "No" to either pre-check is a hard eligibility screen the front-end (Group D,
// Step 7) short-circuits WITHOUT running the calculation — so both are simple
// (always-shown) required radios, not conditional and not calc inputs.
//
// PENDING DESIGNER CONFIRM: the 0826 asset-field labels below come only from the
// two spouse frames; the two result frames still show the old labels (the four
// frames are internally inconsistent). They are labels-only, so they can be
// amended later without touching any key or downstream code. Decisions A (no
// table) and B (keep partnerPwd) are confirmed. See
// document/MYSS-169-0826-Seed-Label-Edits.md.
export const eligibilityEstimatorSpecV2: Json = {
  display: "form",
  components: [
    {
      type: "radio",
      key: "residesInBc",
      label: "Do you currently reside in British Columbia?",
      tooltip:
        "You must be a resident of British Columbia to receive assistance from this ministry.",
      input: true,
      values: yesNoValues,
      validate: { required: true },
    },
    {
      type: "radio",
      key: "hasEligibleStatus",
      label: "Do you have a status that allows you to live in Canada?",
      tooltip:
        "For example a Canadian citizen, permanent resident, Convention refugee, or another immigration status that allows you to live in Canada.",
      input: true,
      values: yesNoValues,
      validate: { required: true },
    },
    {
      type: "radio",
      key: "relationshipStatus",
      label: "What is your relationship status?",
      input: true,
      values: [
        { label: "Single and Never Married", value: "single" },
        { label: "Married", value: "married" },
        { label: "Marriage-Like Relationship", value: "marriagelike" },
        { label: "Divorced", value: "divorced" },
        { label: "Separated", value: "separated" },
        { label: "Widowed", value: "widowed" },
      ],
      validate: { required: true },
    },
    {
      type: "number",
      key: "dependentChildren",
      label: "How many dependent children under the age of 19 live with you?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "radio",
      key: "pwd",
      label:
        "Do you plan to apply for the Persons with Disabilities (PWD) designation?",
      input: true,
      values: yesNoValues,
      validate: { required: true },
    },
    {
      // Advanced-conditional, NOT server-required. Kept exactly as v1
      // (Decision B): reveals on married/marriage-like.
      type: "radio",
      key: "partnerPwd",
      label:
        "Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?",
      input: true,
      values: yesNoValues,
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Your Monthly Income",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "vehicleValueMinusTransportation",
      label:
        "What is the value of your primary vehicle minus any amount owing?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "vehicleValue",
      label:
        "What is the value of all your additional vehicles minus any amount owing?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "assetValue",
      label:
        "What is the total value of your assets not listed above (property, investments, cash or savings)?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
    },
    {
      type: "number",
      key: "partnerMonthlyIncome",
      label: "Spouse's Monthly Income",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "partnerVehicleValueMinusTransportation",
      label:
        "What is the value of your spouse's primary vehicle minus any amount owing?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "partnerVehicleValue",
      label:
        "What is the value of all your spouse's additional vehicles minus any amount owing?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "number",
      key: "partnerAssetValue",
      label:
        "What is the total value of your spouse's assets not listed above (property, investments, cash or savings)?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: partneredConditional,
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Get Estimate",
      input: true,
    },
  ],
};

// ---------------------------------------------------------------------------
// Everything the bootstrap hook seeds
// ---------------------------------------------------------------------------

/** A form the bootstrap hook seeds: a logical id, a title, and its versions. */
export interface SeededForm {
  readonly formSpecId: string;
  readonly title: string;
  readonly versions: readonly SeededFormSpec[];
}

/**
 * Every form the bootstrap hook seeds. Adding a form here (or a version to an
 * existing form's `versions`) is the only change needed to seed more content.
 */
export const seededForms: readonly SeededForm[] = [
  {
    formSpecId: POC_FORM_SPEC_ID,
    title: POC_FORM_SPEC_TITLE,
    versions: seededFormSpecs,
  },
  {
    formSpecId: ELIGIBILITY_ESTIMATOR_FORM_SPEC_ID,
    title: ELIGIBILITY_ESTIMATOR_FORM_SPEC_TITLE,
    versions: [
      { version: 1, spec: eligibilityEstimatorSpecV1 },
      { version: 2, spec: eligibilityEstimatorSpecV2 },
    ],
  },
  {
    formSpecId: BUS_PASS_FORM_SPEC_ID,
    title: BUS_PASS_FORM_SPEC_TITLE,
    versions: [{ version: 1, spec: busPassFormSpec as unknown as Json }],
  },
];
