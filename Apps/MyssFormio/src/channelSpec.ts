import type { FormType } from "@formio/react/lib/components/Form";

import { CHANNEL_RULES_PROPERTY, channelSetting, type RequestChannel } from "./requestChannels";
import { isRecord } from "./specWalk";

// The form as it applies to one request channel; the TypeScript copy of the
// API's ChannelSpec.Resolve. A component whose channel rule has show: false is
// removed with everything inside it, a rule's required becomes
// validate.required, and every properties.myssChannels is dropped.
// Shared/validation/channel-specs.json keeps the two copies equal. It never
// mutates the input.

/** A copy of `spec` resolved for `channel`. */
export function resolveSpecForChannel(spec: FormType, channel: RequestChannel): FormType {
  const resolved = structuredClone(spec);
  resolveChildren(resolved as Record<string, unknown>, channel);
  return resolved;
}

function resolveChildren(node: unknown, channel: RequestChannel): void {
  if (!isRecord(node)) return;
  if (Array.isArray(node.components)) node.components = resolveComponents(node.components, channel);
  resolveColumns(node.columns, channel);
  resolveRows(node.rows, channel);
}

function resolveComponents(components: readonly unknown[], channel: RequestChannel): unknown[] {
  const kept: unknown[] = [];
  for (const component of components) {
    if (isRecord(component)) {
      if (channelSetting(component, channel, "show") === false) continue;
      applyRules(component, channel);
      resolveChildren(component, channel);
    }
    kept.push(component);
  }
  return kept;
}

/** `columns` layout: each column carries its own `components`. */
function resolveColumns(columns: unknown, channel: RequestChannel): void {
  if (!Array.isArray(columns)) return;
  for (const column of columns) resolveChildren(column, channel);
}

/** `rows` (table layout): a row is an array of cells, each with `components`. */
function resolveRows(rows: unknown, channel: RequestChannel): void {
  if (!Array.isArray(rows)) return;
  for (const row of rows) {
    if (!Array.isArray(row)) continue;
    for (const cell of row) resolveChildren(cell, channel);
  }
}

/** Applies the channel's `required` and drops the rules. */
function applyRules(component: Record<string, unknown>, channel: RequestChannel): void {
  const properties = component.properties;
  if (!isRecord(properties) || !(CHANNEL_RULES_PROPERTY in properties)) return;

  const required = channelSetting(component, channel, "required");
  if (required !== undefined) {
    if (!isRecord(component.validate)) component.validate = {};
    (component.validate as Record<string, unknown>).required = required;
  }
  delete properties[CHANNEL_RULES_PROPERTY];
}
