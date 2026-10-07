// Configuration for the Form.io builder: which component types a designer may
// add, and what the component settings dialog exposes.

/** Question types that pick from a fixed set of answers. */
const CHOICE_TYPES = ["radio", "select", "checkbox"] as const;

/** Question types that take a typed answer. */
const TEXT_TYPES = ["textfield", "textarea", "number", "email"] as const;

/** Question types that take a date. */
const DATE_TYPES = ["datetime"] as const;

/**
 * Question types a designer may add. Each is registered with Form.io, drawn
 * with the design system component for it, and type-checked by the API's spec
 * validator, so an answer it collects is still validated server-side on
 * submit. `bcgovRadio` is no longer offered: `radio` renders the same way,
 * and the older name stays registered only for the forms published with it.
 */
const QUESTION_TYPES = [...CHOICE_TYPES, ...TEXT_TYPES, ...DATE_TYPES] as const;

/**
 * Display, action and grouping types, which carry no answer. `button` is here
 * because every seeded spec ends with a submit button that a designer can
 * delete; without a palette entry there would be no way to put one back.
 */
const CONTENT_TYPES = ["bcgovAccordion", "content", "panel", "button"] as const;

/** Every type the palette offers. The builder sorts the sidebar itself. */
export const ALLOWED_COMPONENT_TYPES: readonly string[] = [
  ...QUESTION_TYPES,
  ...CONTENT_TYPES,
];

/**
 * The rules a designer can pick for a field. Each name is a rule the browser
 * (validationRules.ts) and the API (FormSpecValidator) both implement; the
 * value is stored as `properties.myssValidator`, which is where the API has
 * always read it.
 */
export const VALIDATION_RULE_OPTIONS = [
  { label: "None", value: "" },
  { label: "Email address", value: "email" },
  { label: "Phone number", value: "phone" },
  { label: "Postal code", value: "postalCode" },
  { label: "Social Insurance Number", value: "sin" },
  { label: "Date", value: "date" },
  { label: "Day of a day / month / year group", value: "dateParts" },
] as const;

// `true` takes the title, icon and default schema from the component's own
// builderInfo. An unregistered type is dropped from the palette without warning.
function palette(types: readonly string[]): Record<string, true> {
  return Object.fromEntries(types.map((type) => [type, true]));
}

// Disabled rather than removed: the builder derives a new component's key from
// its label by writing into this field, so taking it out leaves new fields
// named textField1. Renaming a key orphans saved answers and any conditional
// pointing at it, so it stays read-only.
const lockedKey = { key: "api", components: [{ key: "key", disabled: true }] };

// "Multiple values" turns the answer into an array, which the API's validator
// rejects for every type offered here. Only the question types carry a Data
// tab; adding this to the others would create an empty one.
const lockedMultiple = {
  key: "data",
  components: [{ key: "multiple", ignore: true }],
};

/**
 * The JavaScript and JSON logic validators, off for every question. Those
 * take code the API cannot run, so a form that relied on them would be
 * validated in the browser alone; the named rules below run on both sides.
 */
const scriptValidatorsOff = [
  { key: "custom-validation-js", ignore: true },
  { key: "json-validation-json", ignore: true },
  { key: "unique", ignore: true },
];

/** The Validation tab for a choice or a checkbox: no script validators. */
const choiceValidationSettings = {
  key: "validation",
  components: scriptValidatorsOff,
};

/**
 * The Validation tab for a typed answer: the standard rule to apply. The
 * rule's wording comes from the error message catalogue, and the dialog's own
 * "Custom Error Message" and "Custom Errors" fields still override it per
 * field. A choice has no use for these rules, so its tab does not offer them.
 */
const textValidationSettings = {
  key: "validation",
  components: [
    {
      type: "select",
      key: "properties.myssValidator",
      label: "Validation rule",
      input: true,
      weight: 5,
      dataSrc: "values",
      data: { values: [...VALIDATION_RULE_OPTIONS] },
      tooltip:
        "A standard check the browser and the server both apply to the answer.",
    },
    {
      type: "textfield",
      key: "properties.myssDateParts.month",
      label: "Key of the month field",
      input: true,
      weight: 6,
      conditional: {
        json: {
          "===": [{ var: "data.properties.myssValidator" }, "dateParts"],
        },
      },
    },
    {
      type: "textfield",
      key: "properties.myssDateParts.year",
      label: "Key of the year field",
      input: true,
      weight: 7,
      conditional: {
        json: {
          "===": [{ var: "data.properties.myssValidator" }, "dateParts"],
        },
      },
    },
    ...scriptValidatorsOff,
  ],
};

// The accordion draws accordionLabel and accordionBody rather than Form.io's
// own label, so the dialog offers those two and drops the label.
const accordionSettings = [
  lockedKey,
  {
    key: "display",
    components: [
      { key: "label", ignore: true },
      {
        type: "textfield",
        key: "accordionLabel",
        label: "Accordion heading",
        input: true,
        weight: 1,
      },
      {
        type: "textarea",
        key: "accordionBody",
        label: "Accordion body",
        description:
          "Basic HTML. Scripts and event handlers are removed when the form renders.",
        input: true,
        rows: 6,
        weight: 2,
      },
    ],
  },
];

const editForm: Record<string, unknown> = {
  ...Object.fromEntries(
    CHOICE_TYPES.map((type) => [
      type,
      [lockedKey, lockedMultiple, choiceValidationSettings],
    ]),
  ),
  ...Object.fromEntries(
    [...TEXT_TYPES, ...DATE_TYPES].map((type) => [
      type,
      [lockedKey, lockedMultiple, textValidationSettings],
    ]),
  ),
  ...Object.fromEntries(CONTENT_TYPES.map((type) => [type, [lockedKey]])),
  bcgovAccordion: accordionSettings,
};

/**
 * Options for `<FormBuilder>`: a palette limited to the types this system can
 * render and validate, and component keys that cannot be edited.
 */
export const builderOptions = {
  noDefaultSubmitButton: true,
  // Without this the palette entries are not reachable by keyboard and a drag
  // is the only way to add a field.
  keyboardBuilder: true,
  builder: {
    // Form.io's own groups, off. Left on, the palette offers dozens of types
    // the API has never heard of and the citizen renderer cannot draw.
    basic: false,
    advanced: false,
    layout: false,
    data: false,
    premium: false,
    // Appended from a Formio project when one resolves, outside the flags above.
    resource: false,
    // Only one group can be open on load; the builder closes the rest.
    choices: {
      title: "Choices",
      weight: 0,
      default: true,
      components: palette(CHOICE_TYPES),
    },
    // An explicit false renders aria-expanded="false"; left out, it is empty.
    text: {
      title: "Text entry",
      weight: 5,
      default: false,
      components: palette([...TEXT_TYPES, ...DATE_TYPES]),
    },
    content: {
      title: "Content and layout",
      weight: 10,
      default: false,
      components: palette(CONTENT_TYPES),
    },
  },
  editForm,
};
