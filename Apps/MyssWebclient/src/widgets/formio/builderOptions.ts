// Configuration for the Form.io builder: which component types a designer may
// add, and what the component settings dialog exposes.

/** Question types that pick from a fixed set of answers. */
const CHOICE_TYPES = ["bcgovRadio", "radio", "select", "checkbox"] as const;

/** Question types that take a typed answer. */
const TEXT_TYPES = ["textfield", "textarea", "number", "email"] as const;

/**
 * Question types a designer may add. Each is registered with Form.io and
 * type-checked by the API's spec validator, so an answer it collects is still
 * validated server-side on submit.
 */
const QUESTION_TYPES = [...CHOICE_TYPES, ...TEXT_TYPES] as const;

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
    QUESTION_TYPES.map((type) => [type, [lockedKey, lockedMultiple]]),
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
      components: palette(TEXT_TYPES),
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
