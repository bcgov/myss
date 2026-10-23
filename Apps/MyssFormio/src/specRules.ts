// Client-side mirror of two cheap checks so an obvious mistake shows
// up as the designer edits, instead of coming back as a server refusal on
// Publish. The authoritative gate is MyssContent's form-spec-rules.ts (run at
// publish time); these two functions are a convenience copy of its duplicate-key
// and unknown-conditional-target rules and must stay in step with it.
// No fetch, no React. Each takes a spec and returns the offending keys.

import { forEachComponent, isRecord } from "./specWalk";

interface ComponentLike {
  readonly key?: unknown;
  readonly conditional?: unknown;
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
