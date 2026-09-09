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

/**
 * Reveal the spouse fields for a partnered relationship (married OR marriage-like).
 *
 * The `var` MUST be `data.relationshipStatus`, not `relationshipStatus`: Form.io
 * evaluates `conditional.json` with jsonLogic against the context
 * `{ data, row, form, _ }`, so the submission answers live under `data`. A bare
 * `relationshipStatus` resolves to undefined and the section never reveals.
 */
const partneredConditional: Json = {
  json: { in: [{ var: "data.relationshipStatus" }, ["married", "marriagelike"]] },
};

// ---------------------------------------------------------------------------
// v3 conditional-display gates (MYSS-206)
// ---------------------------------------------------------------------------
//
// Q2 (hasEligibleStatus) and its help components appear once Q1 (residesInBc)
// has been ANSWERED — either "true" or "false". This must be an ADVANCED
// conditional: "has any value" is not expressible with the simple `{ when, eq }`
// form. As with `partneredConditional`, the `var` MUST be prefixed `data.`.
const q1AnsweredConditional: Json = {
  json: { in: [{ var: "data.residesInBc" }, ["true", "false"]] },
};

// The remaining questions (and the submit button) appear only when Q2 = "Yes".
// A single field/single value gate, so the SIMPLE conditional is enough and
// reads clearly in the admin panel. `validateFormSpec` checks that `when`
// ("hasEligibleStatus") names a real field — it does.
const hasStatusConditional: Json = {
  show: true,
  when: "hasEligibleStatus",
  eq: "true",
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
      type: "panel",
      key: "statusHelp",
      title: 'What does "status that allows you to live in Canada" mean?',
      collapsible: true,
      collapsed: true,
      input: false,
      components: [
        {
          type: "content",
          key: "statusHelpBody",
          input: false,
          html: "<p>For example: a Canadian citizen, permanent resident, Convention refugee, or another immigration status that allows you to live in Canada.</p>",
        },
      ],
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
      description:
        "The estimate is based on a maximum family size of 7 people. Adding more than 7 family members will not change the estimated benefit amount.",
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
      type: "content",
      key: "assetsSectionHeading",
      input: false,
      html: "<h2>Do you have assets or receive income?</h2>",
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
      type: "content",
      key: "spouseSectionHeading",
      input: false,
      html: "<h2>Does your spouse have assets or receive income?</h2>",
      conditional: partneredConditional,
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
// Estimator spec v3 — MYSS-206 conditional display + BC Gov components
// ---------------------------------------------------------------------------
//
// v3 makes three changes on top of v2 (see doc/MYSS-206-Estimator-Conditional-
// Display-Plan.md). v2 stays seeded and immutable; v3 becomes the latest served.
//
//   1. Progressive disclosure. Q2 (hasEligibleStatus) + its help components
//      reveal once Q1 (residesInBc) is answered (`q1AnsweredConditional`); the
//      remaining questions and the submit button reveal only when Q2 = "Yes"
//      (`hasStatusConditional`). Net rule: remaining questions show iff Q2=Yes,
//      regardless of Q1 — Q1=No + Q2=Yes still proceeds (MYSS-206 requirement).
//
//   2. BC Gov components (Option 3). The "What does status mean?" accordion is a
//      custom `bcgovAccordion` component (replacing the v2 Form.io `panel`),
//      rendered by the client's registered custom component (Apps/MyssWebclient
//      src/formio/bcgovComponents.tsx) — a seed<->frontend contract: the type
//      MUST be registered or the form renders a blank slot. MyssApi
//      FormSpecValidator tolerates it as a NonDataType. (The Q2 info tooltip was
//      removed — the explanation lives solely in this accordion now.)
//
//   3. The Q2="No" warning is NOT in the seed. It renders as a React BC Gov
//      InlineAlert in EligibilityEstimatorPage, driven by live form data, so its
//      copy can move to the estimator-content Strapi type (plan §5) without a
//      form-version bump.
//
// Every DATA key is identical to v2 — the mapper contract is unchanged.
export const eligibilityEstimatorSpecV3: Json = {
  display: "form",
  components: [
    {
      type: "radio",
      key: "residesInBc",
      label: "Do you currently reside in British Columbia?",
      input: true,
      values: yesNoValues,
      // dataType "string" keeps the value the literal "true"/"false" the
      // conditionals below match. Without it Form.io's radio defaults to
      // `auto`, which coerces "true"/"false" to booleans — and then
      // `q1AnsweredConditional` / `hasStatusConditional` (which compare against
      // the strings) never match and nothing past Q1 reveals. (`toBool` in the
      // mapper handles either form, so the estimate is unaffected.)
      dataType: "string",
      validate: { required: true },
    },
    {
      type: "radio",
      key: "hasEligibleStatus",
      label: "Do you have a status that allows you to live in Canada?",
      // (Q2 info tooltip removed — the explanation lives in the "What does
      // 'status…' mean?" accordion below instead.)
      input: true,
      values: yesNoValues,
      // See residesInBc: keep the literal "true"/"false" so hasStatusConditional
      // (which gates every remaining question) matches.
      dataType: "string",
      validate: { required: true },
      conditional: q1AnsweredConditional,
    },
    {
      // Custom BC Gov component — the collapsible help (replaces v2's Form.io
      // `panel`). Non-data; label + body carried as component props.
      type: "bcgovAccordion",
      key: "statusHelp",
      input: false,
      accordionLabel:
        'What does "status that allows you to live in Canada" mean?',
      // Body copy per the 0901 design (eligibility-estimator/0901/0901-ee-04.png).
      // "residence requirements" link → gov.bc.ca citizenship-requirements page;
      // this copy moves to the estimator-content Strapi type (§5).
      accordionBody: [
        "<p>To be eligible for assistance, your status must meet the citizenship and residency requirements.</p>",
        "<p>This includes:</p>",
        "<ul>",
        "<li>Canadian citizens</li>",
        "<li>Permanent residents</li>",
        "<li>Protected persons or refugees</li>",
        "<li>Refugee claimants</li>",
        "<li>People with a Temporary Resident Permit</li>",
        "<li>Certain other qualifying statuses</li>",
        "</ul>",
        "<p>If you do not have legal status in Canada, talk to a lawyer before you apply for benefits. If you get benefits that you do not qualify for, you may have to pay the money back.</p>",
        '<p>Learn more about <a href="https://www2.gov.bc.ca/gov/content/governments/policies-for-government/bcea-policy-and-procedure-manual/eligibility/citizenship-requirements" target="_blank" rel="noopener noreferrer">residence requirements for income assistance</a></p>',
      ].join(""),
      conditional: q1AnsweredConditional,
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
      errors: { required: "Please select your relationship status." },
      conditional: hasStatusConditional,
    },
    {
      type: "number",
      key: "dependentChildren",
      label: "How many dependent children under the age of 19 live with you?",
      description:
        "The estimate is based on a maximum family size of 7 people. Adding more than 7 family members will not change the estimated benefit amount.",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: hasStatusConditional,
    },
    {
      type: "radio",
      key: "pwd",
      label:
        "Do you plan to apply for the Persons with Disabilities (PWD) designation?",
      input: true,
      values: yesNoValues,
      validate: { required: true },
      errors: { required: "Please select an option." },
      conditional: hasStatusConditional,
    },
    {
      // Advanced-conditional, NOT server-required (Decision B). Transitively
      // hidden when Q2="No" (relationshipStatus is then hidden + cleared).
      type: "radio",
      key: "partnerPwd",
      label:
        "Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?",
      input: true,
      values: yesNoValues,
      conditional: partneredConditional,
    },
    {
      type: "content",
      key: "assetsSectionHeading",
      input: false,
      html: "<h2>Do you have assets or receive income?</h2>",
      conditional: hasStatusConditional,
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Your Monthly Income",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: hasStatusConditional,
    },
    {
      type: "number",
      key: "vehicleValueMinusTransportation",
      label:
        "What is the value of your primary vehicle minus any amount owing?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: hasStatusConditional,
    },
    {
      type: "number",
      key: "vehicleValue",
      label:
        "What is the value of all your additional vehicles minus any amount owing?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: hasStatusConditional,
    },
    {
      type: "number",
      key: "assetValue",
      label:
        "What is the total value of your assets not listed above (property, investments, cash or savings)?",
      input: true,
      defaultValue: 0,
      validate: { min: 0 },
      conditional: hasStatusConditional,
    },
    {
      type: "content",
      key: "spouseSectionHeading",
      input: false,
      html: "<h2>Does your spouse have assets or receive income?</h2>",
      conditional: partneredConditional,
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
      conditional: hasStatusConditional,
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
      { version: 3, spec: eligibilityEstimatorSpecV3 },
    ],
  },
  {
    formSpecId: BUS_PASS_FORM_SPEC_ID,
    title: BUS_PASS_FORM_SPEC_TITLE,
    versions: [{ version: 1, spec: busPassFormSpec as unknown as Json }],
  },
];
