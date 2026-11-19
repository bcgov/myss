// Client-side mirror of the cheap checks so an obvious mistake shows
// up as the designer edits, instead of coming back as a server refusal on
// Publish. The authoritative gate is MyssContent's form-spec-rules.ts (run at
// publish time); these functions are a convenience copy of its duplicate-key,
// unknown-conditional-target and repeat-section rules and must stay in step
// with it. No fetch, no React.

import { forEachComponent, isRecord } from "./specWalk";

interface ComponentLike {
  readonly key?: unknown;
  readonly type?: unknown;
  readonly properties?: unknown;
  readonly conditional?: unknown;
}

/** A section of questions answered once per unit of a count question. */
const REPEAT_SECTION_TYPE = "bcgovRepeatSection";

/** The highest maximum number of rows a repeat section may set. */
const REPEAT_MAX_CEILING = 50;

/** One problem with a repeat section, worded for the designer. */
export interface RepeatSectionProblem {
  readonly key: string;
  readonly message: string;
}

/** Every component in the tree, in document order (see specWalk). */
function collectComponents(spec: unknown): ComponentLike[] {
  const out: ComponentLike[] = [];
  forEachComponent(spec, (component) => {
    out.push(component);
  });
  return out;
}

/** Component keys that appear more than once anywhere in the form, sorted. */
export function findDuplicateKeys(spec: unknown): string[] {
  const seen = new Set<string>();
  const duplicates = new Set<string>();

  for (const component of collectComponents(spec)) {
    const key = component.key;
    // Blank/typeless keys are a different rule (missing key); skip them here.
    if (typeof key !== "string" || key.trim() === "") continue;
    if (seen.has(key)) duplicates.add(key);
    seen.add(key);
  }

  return [...duplicates].sort((a, b) => a.localeCompare(b));
}

/**
 * `conditional.when` values that name a key no component in the form has, sorted.
 * Only the simple `when` form is a key reference; advanced json-logic
 * conditionals carry no `when` and are ignored (as the server rule does).
 */
export function findUnknownConditionalTargets(spec: unknown): string[] {
  const components = collectComponents(spec);

  const keys = new Set<string>();
  for (const component of components) {
    const key = component.key;
    if (typeof key === "string" && key.trim() !== "") keys.add(key);
  }

  const unknown = new Set<string>();
  for (const component of components) {
    if (!isRecord(component.conditional)) continue;
    const when = component.conditional.when;
    if (typeof when === "string" && when.length > 0 && !keys.has(when)) {
      unknown.add(when);
    }
  }

  return [...unknown].sort((a, b) => a.localeCompare(b));
}

/**
 * Problems with the form's repeat sections, in document order: each names a
 * number question outside itself as its count, sets a maximum from 1 to 50 if
 * it sets one, and holds no repeat section of its own. A keyless section is
 * left to the key rules.
 */
export function findInvalidRepeatSections(spec: unknown): RepeatSectionProblem[] {
  const components = collectComponents(spec);
  const typesByKey = new Map<string, unknown>();
  for (const component of components) {
    if (typeof component.key === "string" && !typesByKey.has(component.key)) {
      typesByKey.set(component.key, component.type);
    }
  }

  const problems: RepeatSectionProblem[] = [];
  for (const section of components) {
    if (section.type !== REPEAT_SECTION_TYPE) continue;
    if (typeof section.key !== "string" || section.key.trim() === "") continue;
    for (const message of repeatSectionProblems(section.key, section, typesByKey)) {
      problems.push({ key: section.key, message });
    }
  }
  return problems;
}

function repeatSectionProblems(
  key: string,
  section: ComponentLike,
  typesByKey: ReadonlyMap<string, unknown>,
): string[] {
  const inside = collectComponents(section);
  const insideKeys = new Set(inside.map((component) => component.key));
  const properties = isRecord(section.properties) ? section.properties : {};
  const problems: string[] = [];

  const countProblem = countQuestionProblem(key, properties.myssRepeatCount, typesByKey, insideKeys);
  if (countProblem) problems.push(countProblem);
  const maximum = maximumProblem(key, properties.myssRepeatMax);
  if (maximum) problems.push(maximum);
  const nested = inside.find((component) => component.type === REPEAT_SECTION_TYPE);
  if (nested) {
    problems.push(`The repeat section "${key}" contains another repeat section, ${JSON.stringify(nested.key ?? "")}.`);
  }
  return problems;
}

function countQuestionProblem(
  key: string,
  count: unknown,
  typesByKey: ReadonlyMap<string, unknown>,
  insideKeys: ReadonlySet<unknown>,
): string | undefined {
  if (typeof count !== "string" || count.trim() === "") {
    return `The repeat section "${key}" needs a count question.`;
  }
  if (!typesByKey.has(count)) {
    return `The repeat section "${key}" counts with "${count}", which is not a field in this form.`;
  }
  if (insideKeys.has(count)) {
    return `The repeat section "${key}" counts with "${count}", which is inside the section.`;
  }
  if (typesByKey.get(count) !== "number") {
    return `The repeat section "${key}" counts with "${count}", which is not a number question.`;
  }
  return undefined;
}

/** A blank maximum means the default; otherwise a whole number from 1 to 50, as a number or as digits. */
function maximumProblem(key: string, maximum: unknown): string | undefined {
  if (maximum === undefined || maximum === null) return undefined;
  if (typeof maximum === "string" && maximum.trim() === "") return undefined;

  const value =
    typeof maximum === "string" && /^\d+$/.test(maximum.trim()) ? Number(maximum.trim()) : maximum;
  if (typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= REPEAT_MAX_CEILING) {
    return undefined;
  }
  return `The repeat section "${key}" has a maximum of ${JSON.stringify(maximum)}; use a whole number from 1 to ${REPEAT_MAX_CEILING}.`;
}
