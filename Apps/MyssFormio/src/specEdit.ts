import type { FormType } from "@formio/react/lib/components/Form";
import {
  CHANNEL_RULES_PROPERTY,
  REQUEST_CHANNELS,
  REQUEST_CHANNEL_IDS,
  channelSetting,
  type RequestChannel,
} from "./requestChannels";
import { forEachComponent, isRecord } from "./specWalk";

// Pure helpers for reading and changing a form spec in the editor. No fetch, no
// React. The setters return a brand-new spec rather than mutating the input, so
// React sees a changed object and re-renders the live preview, and the loaded
// draft stays untouched.

/** One editable field surfaced in the editor panel. */
export interface EditableComponent {
  key: string;
  label: string;
  required: boolean;
  type: string;
}

function isRequired(component: Record<string, unknown>): boolean {
  return isRecord(component.validate) && component.validate.required === true;
}

// Layout components that only group other components — skipped when flattening,
// so each row of the editor is a single rendered field, not a container.
const CONTAINER_TYPES = new Set([
  "panel",
  "well",
  "fieldset",
  "columns",
  "table",
  "tabs",
  "container",
  "datagrid",
  "editgrid",
  "bcgovRepeatSection",
]);

/**
 * Every rendered component in document order, lifted out of its layout
 * containers — one entry per field or display item. Drives the row-by-row
 * editor where each rendered field lines up with its settings.
 */
export function flattenComponents(spec: FormType): Record<string, unknown>[] {
  const out: Record<string, unknown>[] = [];
  forEachComponent(spec, (component) => {
    const type = component.type;
    // Skip layout containers and any typeless wrapper (e.g. a tabs panel); their
    // real children are still collected by the recursion.
    if (typeof type !== "string" || CONTAINER_TYPES.has(type)) return;
    out.push(component);
  });
  return out;
}

// Input components that aren't questions a designer edits: the submit button
// and hidden data carriers both set input:true but have no label to change, and
// a repeat section holds questions rather than being one.
const NON_EDITABLE_TYPES = new Set(["button", "hidden", "bcgovRepeatSection"]);

/** Every input field the designer can edit (label + required), in form order. */
export function listEditableComponents(spec: FormType): EditableComponent[] {
  const out: EditableComponent[] = [];
  forEachComponent(spec, (component) => {
    if (component.input !== true) return;
    if (
      typeof component.type === "string" &&
      NON_EDITABLE_TYPES.has(component.type)
    )
      return;
    if (typeof component.key !== "string" || component.key.trim() === "")
      return;
    out.push({
      key: component.key,
      label: typeof component.label === "string" ? component.label : "",
      required: isRequired(component),
      type: typeof component.type === "string" ? component.type : "",
    });
  });
  return out;
}

function editComponent(
  spec: FormType,
  key: string,
  change: (component: Record<string, unknown>) => void,
): FormType {
  const clone = structuredClone(spec);
  forEachComponent(clone, (component) => {
    if (component.key === key) change(component);
  });
  return clone;
}

/** A copy of `spec` with the label of the component keyed `key` set. */
export function setLabel(spec: FormType, key: string, label: string): FormType {
  return editComponent(spec, key, (component) => {
    component.label = label;
  });
}

/** A copy of `spec` with the required flag of the component keyed `key` set. */
export function setRequired(
  spec: FormType,
  key: string,
  required: boolean,
): FormType {
  return editComponent(spec, key, (component) => {
    const validate = isRecord(component.validate) ? component.validate : {};
    validate.required = required;
    component.validate = validate;
  });
}

/** The keys of the questions the form's repeat sections count with. */
export function repeatCountKeys(spec: unknown): Set<string> {
  const keys = new Set<string>();
  forEachComponent(spec, (component) => {
    const count = isRecord(component.properties)
      ? component.properties.myssRepeatCount
      : undefined;
    if (component.type === "bcgovRepeatSection" && typeof count === "string") {
      keys.add(count);
    }
  });
  return keys;
}

/** The channels a container hides its contents in, each with its name. */
type HiddenBy = ReadonlyMap<RequestChannel, string>;

interface NoteContext {
  readonly notes: Map<string, string[]>;
  readonly counts: ReadonlySet<string>;
}

/**
 * What each keyed component's channel rules do, one phrase per channel, such
 * as "Optional for Worker override": its own rules, or the container that
 * hides it. A repeat section's count question is always required, so its
 * required rules say nothing.
 */
export function describeChannelRules(spec: unknown): Map<string, string[]> {
  const context: NoteContext = {
    notes: new Map(),
    counts: repeatCountKeys(spec),
  };
  describeChildren(spec, new Map(), context);
  return context.notes;
}

function describeChildren(
  node: unknown,
  hiddenBy: HiddenBy,
  context: NoteContext,
): void {
  if (!isRecord(node)) return;
  for (const component of arrayOf(node.components)) {
    if (isRecord(component)) describeComponent(component, hiddenBy, context);
  }
  for (const column of arrayOf(node.columns)) {
    describeChildren(column, hiddenBy, context);
  }
  for (const row of arrayOf(node.rows)) {
    for (const cell of arrayOf(row)) describeChildren(cell, hiddenBy, context);
  }
}

function describeComponent(
  component: Record<string, unknown>,
  hiddenBy: HiddenBy,
  context: NoteContext,
): void {
  const key = typeof component.key === "string" ? component.key : undefined;
  const isCount = key !== undefined && context.counts.has(key);
  const phrases = REQUEST_CHANNELS.map((channel) =>
    channelPhrase(component, channel, hiddenBy, isCount),
  ).filter((phrase): phrase is string => phrase !== undefined);
  if (key !== undefined && phrases.length > 0) context.notes.set(key, phrases);
  describeChildren(component, hidingBelow(component, hiddenBy), context);
}

function channelPhrase(
  component: Record<string, unknown>,
  channel: (typeof REQUEST_CHANNELS)[number],
  hiddenBy: HiddenBy,
  isCount: boolean,
): string | undefined {
  const container = hiddenBy.get(channel.id);
  if (container !== undefined) {
    return `Hidden for ${channel.label} (inside "${container}")`;
  }
  if (channelSetting(component, channel.id, "show") === false) {
    return `Hidden for ${channel.label}`;
  }
  const required = isCount
    ? undefined
    : channelSetting(component, channel.id, "required");
  if (required === undefined) return undefined;
  return `${required ? "Required" : "Optional"} for ${channel.label}`;
}

function hidingBelow(
  component: Record<string, unknown>,
  hiddenBy: HiddenBy,
): HiddenBy {
  const below = new Map(hiddenBy);
  for (const channel of REQUEST_CHANNEL_IDS) {
    if (!below.has(channel) && channelSetting(component, channel, "show") === false) {
      below.set(channel, nameOf(component));
    }
  }
  return below;
}

function nameOf(component: Record<string, unknown>): string {
  for (const name of [component.title, component.label, component.key]) {
    if (typeof name === "string" && name.trim() !== "") return name;
  }
  return "a container";
}

function arrayOf(value: unknown): readonly unknown[] {
  return Array.isArray(value) ? value : [];
}

/** The settings a channel rule may make. */
const CHANNEL_SETTINGS = ["show", "required"] as const;

/**
 * A copy of `spec` without the empty values the builder's Channel rules tab
 * writes for "As authored": the empty setting goes, then a channel left with
 * no setting, then rules left with no channel, then properties left empty.
 * Only the settings of known channels are touched; anything else is left for
 * the rule check to report.
 */
export function dropUnsetChannelRules(spec: FormType): FormType {
  const clone = structuredClone(spec);
  forEachComponent(clone, (component) => {
    const properties = component.properties;
    if (!isRecord(properties)) return;
    const rules = properties[CHANNEL_RULES_PROPERTY];
    if (!isRecord(rules)) return;
    dropUnsetSettings(rules);
    if (Object.keys(rules).length > 0) return;
    delete properties[CHANNEL_RULES_PROPERTY];
    if (Object.keys(properties).length === 0) delete component.properties;
  });
  return clone;
}

function dropUnsetSettings(rules: Record<string, unknown>): void {
  for (const channel of REQUEST_CHANNEL_IDS) {
    const rule = rules[channel];
    if (!isRecord(rule)) continue;
    for (const setting of CHANNEL_SETTINGS) {
      if (rule[setting] === "") delete rule[setting];
    }
    if (Object.keys(rule).length === 0) delete rules[channel];
  }
}

/** How a citizen moves through a form: all on one page, or page by page. */
export type FormLayout = "form" | "wizard";

export function formLayoutOf(spec: FormType): FormLayout {
  return (spec as { display?: unknown }).display === "wizard"
    ? "wizard"
    : "form";
}

function isSubmitButton(component: Record<string, unknown>): boolean {
  return (
    component.type === "button" &&
    (component.action === undefined || component.action === "submit")
  );
}

function unusedKey(spec: FormType, base: string): string {
  const taken = new Set<string>();
  forEachComponent(spec, (component) => {
    if (typeof component.key === "string") taken.add(component.key);
  });
  let key = base;
  let n = 2;
  while (taken.has(key)) {
    key = `${base}${n}`;
    n += 1;
  }
  return key;
}

/**
 * A copy of `spec` in the given layout. A wizard's pages are its top-level
 * panels and it draws its own Submit, so a form without pages is wrapped in
 * one and loses its submit buttons; a one-page form gets a Submit back.
 */
export function setFormLayout(spec: FormType, layout: FormLayout): FormType {
  const clone = structuredClone(spec) as unknown as {
    display?: string;
    components: Record<string, unknown>[];
  };
  clone.display = layout;
  if (layout === "wizard") {
    const kept = clone.components.filter((c) => !isSubmitButton(c));
    const allPages = kept.length > 0 && kept.every((c) => c.type === "panel");
    clone.components = allPages
      ? kept
      : [
          {
            type: "panel",
            key: unusedKey(spec, "page1"),
            title: "Page 1",
            label: "Page 1",
            input: false,
            components: kept,
          },
        ];
  } else if (!clone.components.some(isSubmitButton)) {
    clone.components.push({
      type: "button",
      key: unusedKey(spec, "submit"),
      label: "Submit",
      action: "submit",
      input: true,
    });
  }
  return clone as unknown as FormType;
}
