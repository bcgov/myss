/**
 * Phase 0.3 — publish-time validation rules for form specs.
 *
 * Every function here is pure: it takes the incoming data and the rows that
 * already exist for that `formSpecId`, and returns violation messages. No
 * Strapi, no database, no I/O. The lifecycle hook is a thin wrapper that does
 * one query, calls `evaluateCreate` / `evaluateUpdate`, and turns any returned
 * message into an `ApplicationError` so the admin panel displays it.
 *
 * The behaviour encoded here was MEASURED against a running Strapi 5.51 on
 * 2026-08-17, not inferred from documentation. Three observations drive the
 * design, and breaking any of them breaks ordinary authoring:
 *
 *   1. Publishing is a CREATE, not an update. One publish fires
 *      `beforeUpdate` on the draft row, then `beforeCreate` for a second row
 *      carrying the same `documentId` and a `publishedAt` timestamp.
 *      => The version-sequence rule must run ONLY when `documentId` is null,
 *         or publishing a valid entry gets rejected because the draft already
 *         holds that version. `strapi.documents().create({status:"published"})`
 *         — which our own seeder calls — fires `beforeCreate` twice for the
 *         same reason, so getting this wrong fails the boot on a fresh DB.
 *
 *   2. Editing a published entry updates the DRAFT row, never the published
 *      one. There is no "update to a published row" event to catch.
 *      => Immutability is enforced on draft update, by comparing against the
 *         published sibling that still exists at that moment.
 *
 *   3. Republishing DELETES the old published row before creating the new one,
 *      so at publish-create time there is nothing left to compare against.
 *      => Confirms immutability cannot be enforced at publish time either.
 *
 * `spec` arrives as a STRING from the admin panel and as an OBJECT from the
 * Document Service (the seeder). Both are handled.
 */

import { REQUEST_CHANNELS, isRequestChannel } from "./request-channels";

/** Stable keywords, so messages can later be sourced from content and translated. */
export const FormSpecRule = {
  SpecUnparseable: "FORMSPEC.SPEC.UNPARSEABLE",
  SpecNotAnObject: "FORMSPEC.SPEC.NOT_AN_OBJECT",
  ComponentsMissing: "FORMSPEC.COMPONENTS.MISSING",
  ComponentsEmpty: "FORMSPEC.COMPONENTS.EMPTY",
  ComponentKeyMissing: "FORMSPEC.COMPONENT_KEY.MISSING",
  ComponentKeyDuplicate: "FORMSPEC.COMPONENT_KEY.DUPLICATE",
  ConditionalUnknownField: "FORMSPEC.CONDITIONAL.UNKNOWN_FIELD",
  RepeatSectionInvalid: "FORMSPEC.REPEAT_SECTION.INVALID",
  ChannelRulesInvalid: "FORMSPEC.CHANNEL_RULES.INVALID",
  VersionNotNext: "FORMSPEC.VERSION.NOT_NEXT",
  PublishedImmutable: "FORMSPEC.PUBLISHED.IMMUTABLE",
} as const;

export type FormSpecRuleKeyword = (typeof FormSpecRule)[keyof typeof FormSpecRule];

/** A rule violation: a stable keyword plus a message an author can act on. */
export interface Violation {
  readonly keyword: FormSpecRuleKeyword;
  readonly message: string;
}

/** A row as stored by Strapi. Draft rows have `publishedAt: null`. */
export interface FormSpecRow {
  readonly id?: number;
  readonly documentId?: string | null;
  readonly formSpecId?: string | null;
  readonly version?: number | null;
  readonly publishedAt?: string | Date | null;
  readonly spec?: unknown;
  readonly title?: string | null;
}

/** The `params.data` of a create or update event. */
export interface IncomingFormSpec {
  readonly documentId?: string | null;
  readonly formSpecId?: string | null;
  readonly version?: number | null;
  readonly publishedAt?: string | Date | null;
  readonly spec?: unknown;
  readonly title?: string | null;
}

function violation(keyword: FormSpecRuleKeyword, message: string): Violation {
  return { keyword, message };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Normalises the two shapes `spec` arrives in. A string is parsed; an object
 * is passed through. Anything else, or invalid JSON, is a parse failure.
 */
export function parseSpec(
  spec: unknown,
): { ok: true; value: Record<string, unknown> } | { ok: false; violation: Violation } {
  let candidate: unknown = spec;

  if (typeof spec === "string") {
    try {
      candidate = JSON.parse(spec);
    } catch (error) {
      const detail = error instanceof Error ? error.message : String(error);
      return {
        ok: false,
        violation: violation(
          FormSpecRule.SpecUnparseable,
          `The form spec is not valid JSON: ${detail}`,
        ),
      };
    }
  }

  if (!isRecord(candidate)) {
    return {
      ok: false,
      violation: violation(
        FormSpecRule.SpecNotAnObject,
        "The form spec must be a JSON object with a `components` array.",
      ),
    };
  }

  return { ok: true, value: candidate };
}

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

function collectComponents(spec: Record<string, unknown>): ComponentLike[] {
  const out: ComponentLike[] = [];

  // Form.io nests components inside panels, columns, fieldsets and wizard
  // pages. A rule that only walks the top level would miss most real forms.
  const walk = (nodes: unknown) => {
    if (!Array.isArray(nodes)) return;
    for (const node of nodes) {
      if (!isRecord(node)) continue;
      out.push(node as ComponentLike);
      walk(node.components);
      if (Array.isArray(node.columns)) {
        for (const column of node.columns) {
          if (isRecord(column)) walk(column.components);
        }
      }
      if (Array.isArray(node.rows)) {
        for (const row of node.rows) {
          if (Array.isArray(row)) {
            for (const cell of row) {
              if (isRecord(cell)) walk(cell.components);
            }
          }
        }
      }
    }
  };

  walk(spec.components);
  return out;
}

/** The `when` field of a conditional, when it names another component. */
function conditionalTarget(component: ComponentLike): string | undefined {
  if (!isRecord(component.conditional)) return undefined;
  const when = component.conditional.when;
  return typeof when === "string" && when.length > 0 ? when : undefined;
}

/**
 * Rules (a), (b) and (c) of §8.2: parse as Form.io JSON, reject duplicate or
 * malformed field keys, reject conditionals referencing fields that do not
 * exist.
 */
export function validateFormSpec(spec: unknown): Violation[] {
  const parsed = parseSpec(spec);
  if (!parsed.ok) return [parsed.violation];

  if (!Array.isArray(parsed.value.components)) {
    return [
      violation(
        FormSpecRule.ComponentsMissing,
        "The form spec must have a `components` array.",
      ),
    ];
  }

  const components = collectComponents(parsed.value);
  if (components.length === 0) {
    return [
      violation(FormSpecRule.ComponentsEmpty, "The form spec has no components."),
    ];
  }

  const violations: Violation[] = [];
  const seen = new Set<string>();
  const duplicates = new Set<string>();

  for (const component of components) {
    const key = component.key;
    if (typeof key !== "string" || key.trim() === "") {
      violations.push(
        violation(
          FormSpecRule.ComponentKeyMissing,
          "Every component needs a non-empty `key`.",
        ),
      );
      continue;
    }
    if (seen.has(key)) duplicates.add(key);
    seen.add(key);
  }

  for (const key of [...duplicates].sort()) {
    violations.push(
      violation(
        FormSpecRule.ComponentKeyDuplicate,
        `Duplicate component key "${key}". Keys must be unique across the whole form.`,
      ),
    );
  }

  const unknownTargets = new Set<string>();
  for (const component of components) {
    const target = conditionalTarget(component);
    if (target !== undefined && !seen.has(target)) unknownTargets.add(target);
  }

  for (const target of [...unknownTargets].sort()) {
    violations.push(
      violation(
        FormSpecRule.ConditionalUnknownField,
        `A conditional refers to "${target}", which is not a field in this form.`,
      ),
    );
  }

  violations.push(...repeatSectionViolations(components));
  violations.push(...channelRuleViolations(parsed.value));
  return violations;
}

/**
 * A repeat section names a number question that is not hidden, outside every
 * repeat section, as its count, sets a maximum from 1 to 50 if it sets one, and
 * holds no repeat section of its own. A keyless section is left to the key rule.
 */
function repeatSectionViolations(components: readonly ComponentLike[]): Violation[] {
  const questionsByKey = new Map<string, ComponentLike>();
  for (const component of components) {
    if (typeof component.key === "string" && !questionsByKey.has(component.key)) {
      questionsByKey.set(component.key, component);
    }
  }

  const sections = components.filter(isKeyedSection);
  const owners = sectionsByInnerKey(sections);
  const violations: Violation[] = [];
  for (const section of sections) {
    for (const message of repeatSectionProblems(section.key, section, questionsByKey, owners)) {
      violations.push(violation(FormSpecRule.RepeatSectionInvalid, message));
    }
  }
  return violations;
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
    const component: ComponentLike = section;
    for (const inner of collectComponents(component as Record<string, unknown>)) {
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
  const inside = collectComponents(section as Record<string, unknown>);
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

/** Where a component's channel rules are authored, under `properties`. */
const CHANNEL_RULES_PROPERTY = "myssChannels";

/** The settings a channel rule may make. */
const CHANNEL_SETTINGS: readonly string[] = ["show", "required"];

/** A component with the channels it is hidden in, by its own rules or a container's. */
interface PlacedComponent {
  readonly component: ComponentLike;
  readonly key: string | undefined;
  readonly hiddenIn: ReadonlySet<string>;
}

/**
 * A component's channel rules are an object keyed by known channels, each
 * setting only `show` and `required`, as true or false; and a component shown
 * in a channel does not depend on a component hidden in it: through
 * `conditional.when`, `myssMatches`, a day's `myssDateParts`, or as a repeat
 * section's count question. A keyless component is left to the key rule.
 */
function channelRuleViolations(spec: Record<string, unknown>): Violation[] {
  const placed = placeComponents(spec);
  const byKey = new Map<string, PlacedComponent>();
  for (const item of placed) {
    if (item.key !== undefined && !byKey.has(item.key)) byKey.set(item.key, item);
  }

  const violations: Violation[] = [];
  for (const item of placed) {
    if (item.key === undefined) continue;
    const messages = [
      ...channelRuleProblems(item.key, item.component),
      ...channelDependencyProblems(item, byKey),
    ];
    for (const message of messages) violations.push(violation(FormSpecRule.ChannelRulesInvalid, message));
  }
  return violations;
}

function placeComponents(spec: Record<string, unknown>): PlacedComponent[] {
  const out: PlacedComponent[] = [];
  placeChildren(spec, new Set(), out);
  return out;
}

/** Places every component below `node`, in document order: components, columns and table cells. */
function placeChildren(node: unknown, hiddenAbove: ReadonlySet<string>, out: PlacedComponent[]): void {
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

function keyOf(component: ComponentLike): string | undefined {
  return typeof component.key === "string" && component.key.trim() !== "" ? component.key : undefined;
}

/** The authored rules; `null` counts as none. */
function channelRulesOf(component: ComponentLike): unknown {
  return isRecord(component.properties) ? component.properties[CHANNEL_RULES_PROPERTY] : undefined;
}

function channelsHiddenIn(component: ComponentLike, hiddenAbove: ReadonlySet<string>): Set<string> {
  const hidden = new Set(hiddenAbove);
  const rules = channelRulesOf(component);
  if (!isRecord(rules)) return hidden;
  for (const channel of REQUEST_CHANNELS) {
    const rule = rules[channel];
    if (isRecord(rule) && rule.show === false) hidden.add(channel);
  }
  return hidden;
}

function channelRuleProblems(key: string, component: ComponentLike): string[] {
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
  const isHiddenIn = (key: string | undefined, channel: string) =>
    key !== undefined && byKey.get(key)?.hiddenIn.has(channel) === true;

  const problems: string[] = [];
  for (const channel of REQUEST_CHANNELS) {
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
function dependencyTargets(component: ComponentLike): string[] {
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

function repeatCountOf(section: ComponentLike): string | undefined {
  const count = isRecord(section.properties) ? section.properties.myssRepeatCount : undefined;
  return typeof count === "string" && count.trim() !== "" ? count : undefined;
}

/**
 * True when this create is a genuinely new document rather than Strapi
 * materialising a published row for a document that already exists.
 *
 * MEASURED: a real create has `documentId === null` in `params.data`; the
 * publish-create carries the existing `documentId`.
 */
export function isNewDocument(data: IncomingFormSpec): boolean {
  return data.documentId === null || data.documentId === undefined;
}

/** Rule (d): the version must be exactly `max(version) + 1` for this form. */
export function assertVersionSequence(
  data: IncomingFormSpec,
  siblings: readonly FormSpecRow[],
): Violation[] {
  const version = data.version;
  if (typeof version !== "number" || !Number.isInteger(version) || version < 1) {
    return [
      violation(
        FormSpecRule.VersionNotNext,
        "Version must be a whole number of 1 or more.",
      ),
    ];
  }

  const versions = siblings
    .map((row) => row.version)
    .filter((value): value is number => typeof value === "number");

  // Draft and published rows of the same version both appear here; the max is
  // what matters, so duplicates are harmless.
  const expected = versions.length === 0 ? 1 : Math.max(...versions) + 1;

  if (version !== expected) {
    return [
      violation(
        FormSpecRule.VersionNotNext,
        `Version must be ${expected} for "${data.formSpecId}" — versions are sequential and ` +
          `${versions.length === 0 ? "this is the first" : `the highest so far is ${Math.max(...versions)}`}. ` +
          `Received ${version}.`,
      ),
    ];
  }

  return [];
}

function isPublished(row: FormSpecRow): boolean {
  return row.publishedAt !== null && row.publishedAt !== undefined;
}

/** The published row of a document, if one exists. */
export function findPublishedSibling(
  documentId: string | null | undefined,
  siblings: readonly FormSpecRow[],
): FormSpecRow | undefined {
  if (!documentId) return undefined;
  return siblings.find((row) => row.documentId === documentId && isPublished(row));
}

/** Key-order-insensitive comparison, so a reserialised spec is not a "change". */
export function canonicalJson(value: unknown): string {
  const normalise = (input: unknown): unknown => {
    if (Array.isArray(input)) return input.map(normalise);
    if (isRecord(input)) {
      const out: Record<string, unknown> = {};
      for (const key of Object.keys(input).sort()) out[key] = normalise(input[key]);
      return out;
    }
    return input;
  };

  const parsed = parseSpec(value);
  return JSON.stringify(normalise(parsed.ok ? parsed.value : value));
}

/**
 * Rule (e): a published version is immutable. `title` is deliberately NOT
 * immutable — it is a label in the admin listing, not part of the contract a
 * submission was rendered against. `spec`, `version` and `formSpecId` are.
 */
export function checkImmutability(
  data: IncomingFormSpec,
  published: FormSpecRow,
): Violation[] {
  const violations: Violation[] = [];
  const nextVersion = (published.version ?? 0) + 1;

  if (data.spec !== undefined && canonicalJson(data.spec) !== canonicalJson(published.spec)) {
    violations.push(
      violation(
        FormSpecRule.PublishedImmutable,
        `Version ${published.version} of "${published.formSpecId}" is published and cannot be ` +
          `changed — submissions were rendered against it. Create version ${nextVersion} instead.`,
      ),
    );
  }

  if (data.version !== undefined && data.version !== published.version) {
    violations.push(
      violation(
        FormSpecRule.PublishedImmutable,
        `The version of a published entry cannot be changed. Create version ${nextVersion} as a ` +
          `new entry instead.`,
      ),
    );
  }

  if (data.formSpecId !== undefined && data.formSpecId !== published.formSpecId) {
    violations.push(
      violation(
        FormSpecRule.PublishedImmutable,
        "The form id of a published entry cannot be changed.",
      ),
    );
  }

  return violations;
}

/**
 * Everything the hook enforces on create.
 *
 * `siblings` must be every row sharing this `formSpecId`, drafts included.
 */
export function evaluateCreate(
  data: IncomingFormSpec,
  siblings: readonly FormSpecRow[],
): Violation[] {
  const violations: Violation[] = [];

  if (data.spec !== undefined) violations.push(...validateFormSpec(data.spec));

  // Skipped for the publish-create: the draft row already holds this version,
  // so a sequence check here would reject a valid publish.
  if (isNewDocument(data)) violations.push(...assertVersionSequence(data, siblings));

  return violations;
}

/** Everything the hook enforces on update. */
export function evaluateUpdate(
  data: IncomingFormSpec,
  siblings: readonly FormSpecRow[],
): Violation[] {
  const violations: Violation[] = [];

  if (data.spec !== undefined) violations.push(...validateFormSpec(data.spec));

  const published = findPublishedSibling(data.documentId, siblings);
  if (published) violations.push(...checkImmutability(data, published));

  return violations;
}

/** One line per violation, for an admin-panel error message. */
export function formatViolations(violations: readonly Violation[]): string {
  return violations.map((v) => v.message).join(" ");
}
