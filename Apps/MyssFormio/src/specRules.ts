// Client-side mirror of the cheap checks so an obvious mistake shows
// up as the designer edits, instead of coming back as a server refusal on
// Publish. The authoritative gate is MyssContent's form-spec-rules.ts (run at
// publish time); these functions are a convenience copy of its duplicate-key,
// unknown-conditional-target, repeat-section and channel rules and must stay in
// step with it. No fetch, no React.

import {
  REQUEST_CHANNEL_IDS,
  channelRulesOf,
  channelSetting,
  isRequestChannel,
  type RequestChannel,
} from "./requestChannels";
import { forEachComponent, isRecord } from "./specWalk";

interface ComponentLike {
  readonly key?: unknown;
  readonly type?: unknown;
  readonly properties?: unknown;
  readonly conditional?: unknown;
  readonly hidden?: unknown;
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
 * number question that is not hidden, outside every repeat section, as its
 * count, sets a maximum from 1 to 50 if it sets one, and holds no repeat
 * section of its own. A keyless section is left to the key rules.
 */
export function findInvalidRepeatSections(spec: unknown): RepeatSectionProblem[] {
  const components = collectComponents(spec);
  const questionsByKey = new Map<string, ComponentLike>();
  for (const component of components) {
    if (typeof component.key === "string" && !questionsByKey.has(component.key)) {
      questionsByKey.set(component.key, component);
    }
  }

  const sections = components.filter(isKeyedSection);
  const owners = sectionsByInnerKey(sections);
  const problems: RepeatSectionProblem[] = [];
  for (const section of sections) {
    for (const message of repeatSectionProblems(section.key, section, questionsByKey, owners)) {
      problems.push({ key: section.key, message });
    }
  }
  return problems;
}

function isKeyedSection(component: ComponentLike): component is ComponentLike & { key: string } {
  return (
    component.type === REPEAT_SECTION_TYPE &&
    typeof component.key === "string" &&
    component.key.trim() !== ""
  );
}

/** The repeat section each question inside one belongs to, by the question's key. */
function sectionsByInnerKey(sections: readonly (ComponentLike & { key: string })[]): Map<unknown, string> {
  const owners = new Map<unknown, string>();
  for (const section of sections) {
    for (const inner of collectComponents(section)) {
      if (!owners.has(inner.key)) owners.set(inner.key, section.key);
    }
  }
  return owners;
}

function repeatSectionProblems(
  key: string,
  section: ComponentLike,
  questionsByKey: ReadonlyMap<string, ComponentLike>,
  owners: ReadonlyMap<unknown, string>,
): string[] {
  const inside = collectComponents(section);
  const insideKeys = new Set(inside.map((component) => component.key));
  const properties = isRecord(section.properties) ? section.properties : {};
  const problems: string[] = [];

  const countProblem = countQuestionProblem(key, properties.myssRepeatCount, questionsByKey, insideKeys, owners);
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
  questionsByKey: ReadonlyMap<string, ComponentLike>,
  insideKeys: ReadonlySet<unknown>,
  owners: ReadonlyMap<unknown, string>,
): string | undefined {
  if (typeof count !== "string" || count.trim() === "") {
    return `The repeat section "${key}" needs a count question.`;
  }
  const question = questionsByKey.get(count);
  if (!question) {
    return `The repeat section "${key}" counts with "${count}", which is not a field in this form.`;
  }
  if (insideKeys.has(count)) {
    return `The repeat section "${key}" counts with "${count}", which is inside the section.`;
  }
  const owner = owners.get(count);
  if (owner !== undefined) {
    return `The repeat section "${key}" counts with "${count}", which is inside the repeat section "${owner}".`;
  }
  if (question.type !== "number") {
    return `The repeat section "${key}" counts with "${count}", which is not a number question.`;
  }
  if (question.hidden === true) {
    return `The repeat section "${key}" counts with "${count}", which is hidden.`;
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

/** The settings a channel rule may make. */
const CHANNEL_SETTINGS: readonly string[] = ["show", "required"];

/** One problem with a component's channel rules, worded for the designer. */
export interface ChannelRuleProblem {
  readonly key: string;
  readonly message: string;
}

/** A component with the channels it is hidden in, by its own rules or a container's. */
interface PlacedComponent {
  readonly component: Record<string, unknown>;
  readonly key: string | undefined;
  readonly hiddenIn: ReadonlySet<RequestChannel>;
}

/**
 * Problems with the form's channel rules, component by component in document
 * order: each component's rules are an object keyed by known channels, each
 * setting only `show` and `required`, as true or false; and a component shown
 * in a channel does not depend on a component hidden in it: through
 * `conditional.when`, `myssMatches`, a day's `myssDateParts`, or as a repeat
 * section's count question. A keyless component is left to the key rules.
 */
export function findInvalidChannelRules(spec: unknown): ChannelRuleProblem[] {
  const placed = placeComponents(spec);
  const byKey = new Map<string, PlacedComponent>();
  for (const item of placed) {
    if (item.key !== undefined && !byKey.has(item.key)) byKey.set(item.key, item);
  }

  const problems: ChannelRuleProblem[] = [];
  for (const item of placed) {
    const key = item.key;
    if (key === undefined) continue;
    const messages = [...channelRuleProblems(key, item.component), ...channelDependencyProblems(item, byKey)];
    for (const message of messages) problems.push({ key, message });
  }
  return problems;
}

function placeComponents(spec: unknown): PlacedComponent[] {
  const out: PlacedComponent[] = [];
  placeChildren(spec, new Set(), out);
  return out;
}

/** Places every component below `node`, in document order: components, columns and table cells. */
function placeChildren(node: unknown, hiddenAbove: ReadonlySet<RequestChannel>, out: PlacedComponent[]): void {
  if (!isRecord(node)) return;
  for (const component of arrayOf(node.components)) {
    if (!isRecord(component)) continue;
    const hiddenIn = channelsHiddenIn(component, hiddenAbove);
    out.push({ component, key: keyOf(component), hiddenIn });
    placeChildren(component, hiddenIn, out);
  }
  for (const column of arrayOf(node.columns)) placeChildren(column, hiddenAbove, out);
  for (const row of arrayOf(node.rows)) {
    for (const cell of arrayOf(row)) placeChildren(cell, hiddenAbove, out);
  }
}

function arrayOf(value: unknown): readonly unknown[] {
  return Array.isArray(value) ? value : [];
}

function keyOf(component: Record<string, unknown>): string | undefined {
  return typeof component.key === "string" && component.key.trim() !== "" ? component.key : undefined;
}

function channelsHiddenIn(
  component: Record<string, unknown>,
  hiddenAbove: ReadonlySet<RequestChannel>,
): Set<RequestChannel> {
  const hidden = new Set(hiddenAbove);
  for (const channel of REQUEST_CHANNEL_IDS) {
    if (channelSetting(component, channel, "show") === false) hidden.add(channel);
  }
  return hidden;
}

function channelRuleProblems(key: string, component: Record<string, unknown>): string[] {
  const rules = channelRulesOf(component);
  if (rules === undefined || rules === null) return [];
  if (!isRecord(rules)) return [`The channel rules of "${key}" must be an object keyed by channel.`];
  return Object.entries(rules).flatMap(([channel, rule]) => oneChannelRuleProblems(key, channel, rule));
}

function oneChannelRuleProblems(key: string, channel: string, rule: unknown): string[] {
  if (!isRequestChannel(channel)) {
    return [`The channel rules of "${key}" name "${channel}", which is not a channel.`];
  }
  if (!isRecord(rule)) {
    return [`The channel rule of "${key}" for "${channel}" must be an object.`];
  }

  const problems: string[] = [];
  for (const [setting, value] of Object.entries(rule)) {
    if (!CHANNEL_SETTINGS.includes(setting)) {
      problems.push(`The channel rule of "${key}" for "${channel}" has "${setting}"; only show and required are allowed.`);
    } else if (typeof value !== "boolean") {
      problems.push(`The channel rule of "${key}" for "${channel}" must set "${setting}" to true or false.`);
    }
  }
  return problems;
}

function channelDependencyProblems(
  item: PlacedComponent,
  byKey: ReadonlyMap<string, PlacedComponent>,
): string[] {
  const targets = dependencyTargets(item.component);
  const count = item.component.type === REPEAT_SECTION_TYPE ? repeatCountOf(item.component) : undefined;
  const isHiddenIn = (key: string | undefined, channel: RequestChannel) =>
    key !== undefined && byKey.get(key)?.hiddenIn.has(channel) === true;

  const problems: string[] = [];
  for (const channel of REQUEST_CHANNEL_IDS) {
    if (item.hiddenIn.has(channel)) continue;
    for (const target of targets.filter((t) => isHiddenIn(t, channel))) {
      problems.push(`"${item.key}" depends on "${target}", which is hidden for the ${channel} channel.`);
    }
    if (isHiddenIn(count, channel)) {
      problems.push(`The repeat section "${item.key}" counts with "${count}", which is hidden for the ${channel} channel.`);
    }
  }
  return problems;
}

/**
 * The components this one reads, each once: its conditional's field, the email
 * it confirms, and, for a day, its month and year.
 */
function dependencyTargets(component: Record<string, unknown>): string[] {
  const properties: Record<string, unknown> = isRecord(component.properties) ? component.properties : {};
  const dateParts: Record<string, unknown> =
    properties.myssValidator === "dateParts" && isRecord(properties.myssDateParts) ? properties.myssDateParts : {};
  const targets = [
    conditionalTarget(component),
    nonEmptyString(properties.myssMatches),
    nonEmptyString(dateParts.month),
    nonEmptyString(dateParts.year),
  ];
  return [...new Set(targets.filter((target): target is string => target !== undefined))];
}

function nonEmptyString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function conditionalTarget(component: Record<string, unknown>): string | undefined {
  if (!isRecord(component.conditional)) return undefined;
  const when = component.conditional.when;
  return typeof when === "string" && when.length > 0 ? when : undefined;
}

function repeatCountOf(section: Record<string, unknown>): string | undefined {
  const count = isRecord(section.properties) ? section.properties.myssRepeatCount : undefined;
  return typeof count === "string" && count.trim() !== "" ? count : undefined;
}
