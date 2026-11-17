import type { FormType } from "@formio/react/lib/components/Form";

import { ALLOWED_COMPONENT_TYPES, QUESTION_TYPES } from "./builderOptions";
import { findDuplicateKeys, findUnknownConditionalTargets } from "./specRules";
import { forEachComponent, isRecord } from "./specWalk";

// Turns text an administrator pastes into a Form.io spec ready to preview and
// save, or into problems that point at where they are. These checks catch
// mistakes early; they are not a security boundary, and the API still checks
// the structure on every save. No fetch, no React.

/** Something in the pasted JSON that blocks saving. */
export interface SpecProblem {
  readonly message: string;
  /** The key of the component the problem is about, when there is one. */
  readonly key?: string;
  /** Where a JSON syntax error is, 1-based, when the browser reports it. */
  readonly line?: number;
  readonly column?: number;
}

export type ParseResult =
  | { readonly state: "empty" }
  | { readonly state: "invalid"; readonly problem: SpecProblem }
  | { readonly state: "parsed"; readonly value: unknown };

export interface SpecCheck {
  readonly problems: readonly SpecProblem[];
  /** Findings that do not block saving. */
  readonly warnings: readonly string[];
  /** Keys of questions in the current version that the pasted spec does not have. */
  readonly removedQuestions: readonly string[];
  /** The number of questions: components that collect an answer. */
  readonly questionCount: number;
}

export interface TextPosition {
  readonly line: number;
  readonly column: number;
}

// Form.io evaluates these as JavaScript, in the citizen's browser and in the preview.
const SCRIPT_PROPERTIES = [
  "calculateValue",
  "customConditional",
  "customDefaultValue",
] as const;
const SCRIPT_TARGETS = new Set<string>([
  ...SCRIPT_PROPERTIES,
  "validate.custom",
]);
const DATA_ONLY_TRIGGERS = new Set(["simple", "json"]);
const BUTTON_ACTIONS = new Set(["submit", "reset", "event"]);
// Child components are visited on their own, so their text is not scanned twice.
const NESTED_PROPERTIES = new Set(["components", "columns", "rows"]);
// Form.io runs `{{ }}` and `{% %}` in strings as JavaScript templates.
const TEMPLATE_MARKERS = ["{{", "{%"];
// Top-level `controller`, `module` and `settings` can run code or load a page.
const TOP_LEVEL_PROPERTIES = new Set(["display", "components"]);
// Types that collect an answer even when a spec leaves `input` out, as Form.io
// fills it in: the palette's questions and the older types the seeded forms use.
const ANSWER_TYPES = new Set<string>([
  ...QUESTION_TYPES,
  "bcgovRadio",
  "phoneNumber",
  "bcgovAddressAutocomplete",
]);

/** The example form offered by Insert example: three questions, one shown conditionally. */
export const EXAMPLE_SPEC: FormType = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "applicantName",
      label: "Your full name",
      input: true,
      validate: { required: true },
    },
    {
      type: "radio",
      key: "inCare",
      label: "Is your child in licensed care?",
      input: true,
      validate: { required: true },
      values: [
        { label: "Yes", value: "yes" },
        { label: "No", value: "no" },
      ],
    },
    {
      type: "textfield",
      key: "careProvider",
      label: "Name of the care provider",
      input: true,
      validate: { required: true },
      conditional: { show: true, when: "inCare", eq: "yes" },
    },
    {
      type: "button",
      key: "submit",
      label: "Submit",
      action: "submit",
      input: true,
    },
  ],
};

/** Parses the text box's content. */
export function parseSpecJson(text: string): ParseResult {
  if (text.trim() === "") return { state: "empty" };
  try {
    return { state: "parsed", value: JSON.parse(text) as unknown };
  } catch (error) {
    const position = locateJsonError(
      error instanceof Error ? error.message : "",
      text,
    );
    return {
      state: "invalid",
      problem: position
        ? {
            message: `The JSON is not valid at line ${position.line}, column ${position.column}.`,
            ...position,
          }
        : { message: "The JSON is not valid." },
    };
  }
}

/**
 * Where a JSON.parse error is, from its message: "(line N column M)" (Chromium),
 * "line N column M of the JSON data" (Firefox) or "at position N" (older
 * engines), counted out in `text`. Anchored, because some messages quote the text.
 */
export function locateJsonError(
  message: string,
  text: string,
): TextPosition | undefined {
  const lineColumn =
    /\(line (\d+) column (\d+)\)$/.exec(message) ??
    /at line (\d+) column (\d+) of the JSON data$/.exec(message);
  if (lineColumn) {
    return { line: Number(lineColumn[1]), column: Number(lineColumn[2]) };
  }
  const offset = /at position (\d+)$/.exec(message);
  return offset ? positionOf(text, Number(offset[1])) : undefined;
}

/** Where the first `"key": "<key>"` is in the text. */
export function locateKey(text: string, key: string): TextPosition | undefined {
  const pattern = new RegExp(`"key"\\s*:\\s*"${escapeRegExp(key)}"`);
  const match = pattern.exec(text);
  return match ? positionOf(text, match.index) : undefined;
}

/** The JSON re-indented with two spaces, or null when it does not parse. */
export function formatSpecJson(text: string): string | null {
  const parsed = parseSpecJson(text);
  return parsed.state === "parsed"
    ? JSON.stringify(parsed.value, null, 2)
    : null;
}

/**
 * Checks a parsed spec. For an existing form, `current` is its current version:
 * the types it uses are allowed, and its questions missing from `value` are reported.
 */
export function checkSpec(value: unknown, current?: unknown): SpecCheck {
  const shapeProblem = specShapeProblem(value);
  if (shapeProblem) {
    return {
      problems: [shapeProblem],
      warnings: [],
      removedQuestions: [],
      questionCount: 0,
    };
  }

  const allowedTypes = new Set(ALLOWED_COMPONENT_TYPES);
  forEachComponent(current, (component) => {
    if (typeof component.type === "string") allowedTypes.add(component.type);
  });

  const problems: SpecProblem[] = [];
  let hasSubmitButton = false;
  forEachComponent(value, (component) => {
    problems.push(...componentProblems(component, allowedTypes));
    if (component.type === "button" && isSubmitAction(component.action)) {
      hasSubmitButton = true;
    }
  });
  problems.push(...keyProblems(value));

  const questions = questionKeys(value);
  const pasted = new Set(questions);
  return {
    problems,
    warnings: hasSubmitButton
      ? []
      : ["There is no Submit button, so citizens could not send this form."],
    removedQuestions:
      current === undefined
        ? []
        : questionKeys(current).filter((key) => !pasted.has(key)),
    questionCount: questions.length,
  };
}

function specShapeProblem(value: unknown): SpecProblem | undefined {
  if (!isRecord(value)) {
    return { message: 'The JSON must be an object with a "components" list.' };
  }
  if (!Array.isArray(value.components)) {
    return { message: 'The JSON has no "components" list.' };
  }
  if (value.components.length === 0) {
    return { message: 'The "components" list is empty.' };
  }
  const extra = Object.keys(value).find(
    (name) => !TOP_LEVEL_PROPERTIES.has(name),
  );
  if (extra !== undefined) {
    return {
      message: `The JSON has a top-level "${extra}", which is not allowed. Only "display" and "components" are.`,
    };
  }
  if (value.display !== undefined && value.display !== "form") {
    return {
      message: `The display ${describe(value.display)} is not allowed. Use "form".`,
    };
  }
  return undefined;
}

function componentProblems(
  component: Record<string, unknown>,
  allowedTypes: ReadonlySet<string>,
): SpecProblem[] {
  const key = keyOf(component);
  const name = key === undefined ? "A component" : `"${key}"`;
  const problems: SpecProblem[] = [];
  const add = (message: string) =>
    problems.push({
      message: `${name}: ${message}`,
      ...(key === undefined ? {} : { key }),
    });

  const type = component.type;
  const typeProblem = componentTypeProblem(type, allowedTypes);
  if (typeProblem) add(typeProblem);

  for (const property of SCRIPT_PROPERTIES) {
    if (isSet(component[property]))
      add(`"${property}" runs code and is not allowed.`);
  }
  if (isRecord(component.validate) && isSet(component.validate.custom)) {
    add('"validate.custom" runs code and is not allowed.');
  }

  const logic = logicProblem(component.logic);
  if (logic) add(logic);

  if (component.dataSrc !== undefined && component.dataSrc !== "values") {
    add(
      `the list source ${describe(component.dataSrc)} is not allowed. Use fixed values.`,
    );
  }
  if (
    type === "button" &&
    component.action !== undefined &&
    !isAllowedAction(component.action)
  ) {
    add(`the button action ${describe(component.action)} is not allowed.`);
  }
  if (component.multiple === true) {
    add('"multiple" makes the answer a list, which the form cannot accept.');
  }
  if (hasInterpolation(component)) {
    add('text containing "{{" or "{%" runs code and is not allowed.');
  }
  return problems;
}

function componentTypeProblem(
  type: unknown,
  allowedTypes: ReadonlySet<string>,
): string | undefined {
  if (typeof type !== "string") return 'it has no "type".';
  if (type === "bcgovRadio" && !allowedTypes.has(type)) {
    return 'use "radio" instead of "bcgovRadio". It looks the same to citizens.';
  }
  if (!allowedTypes.has(type)) return `the type "${type}" is not allowed.`;
  return undefined;
}

/** Only data-only logic: `simple` or `json` triggers and `property` actions. */
function logicProblem(logic: unknown): string | undefined {
  if (logic === undefined || logic === null) return undefined;
  if (!Array.isArray(logic)) return '"logic" must be a list.';

  for (const entry of logic) {
    const problem = logicEntryProblem(entry);
    if (problem) return problem;
  }
  return undefined;
}

function logicEntryProblem(entry: unknown): string | undefined {
  const trigger =
    isRecord(entry) && isRecord(entry.trigger) ? entry.trigger.type : undefined;
  if (typeof trigger !== "string" || !DATA_ONLY_TRIGGERS.has(trigger)) {
    return `logic with the trigger ${describe(trigger)} is not allowed. Only "simple" and "json" triggers are.`;
  }
  const actions =
    isRecord(entry) && Array.isArray(entry.actions) ? entry.actions : [];
  for (const action of actions) {
    const problem = logicActionProblem(action);
    if (problem) return problem;
  }
  return undefined;
}

function logicActionProblem(action: unknown): string | undefined {
  const actionType = isRecord(action) ? action.type : undefined;
  if (actionType !== "property") {
    return `the logic action ${describe(actionType)} is not allowed. Only "property" actions are.`;
  }
  const target =
    isRecord(action) && isRecord(action.property)
      ? action.property.value
      : undefined;
  if (typeof target === "string" && SCRIPT_TARGETS.has(target)) {
    return `a logic action sets "${target}", which runs code.`;
  }
  return undefined;
}

function keyProblems(spec: unknown): SpecProblem[] {
  const problems: SpecProblem[] = findDuplicateKeys(spec).map((key) => ({
    key,
    message: `"${key}" is used as the key of more than one field. Each key must be unique.`,
  }));

  const unknownTargets = new Set(findUnknownConditionalTargets(spec));
  forEachComponent(spec, (component) => {
    const when = isRecord(component.conditional)
      ? component.conditional.when
      : undefined;
    if (typeof when !== "string" || !unknownTargets.has(when)) return;
    const key = keyOf(component);
    problems.push({
      message: `${key === undefined ? "A component" : `"${key}"`}: its condition refers to "${when}", which is not in the form.`,
      ...(key === undefined ? {} : { key }),
    });
  });
  return problems;
}

/** Keys of the components that collect an answer, in document order. */
function questionKeys(spec: unknown): string[] {
  const keys: string[] = [];
  forEachComponent(spec, (component) => {
    const key = keyOf(component);
    if (isQuestion(component) && key !== undefined && !keys.includes(key)) {
      keys.push(key);
    }
  });
  return keys;
}

function isQuestion(component: Record<string, unknown>): boolean {
  if (component.type === "button" || component.input === false) return false;
  return (
    component.input === true ||
    (typeof component.type === "string" && ANSWER_TYPES.has(component.type))
  );
}

function keyOf(component: Record<string, unknown>): string | undefined {
  return typeof component.key === "string" && component.key !== ""
    ? component.key
    : undefined;
}

// Form.io leaves unused script properties out or empty; only a real value counts.
function isSet(value: unknown): boolean {
  return (
    value !== undefined && value !== null && value !== "" && value !== false
  );
}

function isSubmitAction(action: unknown): boolean {
  return action === undefined || action === "submit";
}

function isAllowedAction(action: unknown): boolean {
  return typeof action === "string" && BUTTON_ACTIONS.has(action);
}

function hasInterpolation(component: Record<string, unknown>): boolean {
  return Object.entries(component).some(
    ([property, value]) =>
      !NESTED_PROPERTIES.has(property) && containsInterpolation(value),
  );
}

function containsInterpolation(value: unknown): boolean {
  if (typeof value === "string") {
    return TEMPLATE_MARKERS.some((marker) => value.includes(marker));
  }
  if (Array.isArray(value)) return value.some(containsInterpolation);
  if (isRecord(value)) return Object.values(value).some(containsInterpolation);
  return false;
}

function describe(value: unknown): string {
  return value === undefined ? "(none)" : JSON.stringify(value);
}

function positionOf(text: string, offset: number): TextPosition {
  const before = text.slice(0, offset).split("\n");
  return { line: before.length, column: before[before.length - 1].length + 1 };
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
