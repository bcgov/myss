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

export type Json = string | number | boolean | null | Json[] | { [key: string]: Json };

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
