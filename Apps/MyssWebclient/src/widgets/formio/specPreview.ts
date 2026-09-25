import type { FormType } from "@formio/react/lib/components/Form";

// A display-only transform of a form spec: it removes the conditional-visibility
// rules so every component renders in the editor preview, instead of the
// progressive disclosure that hides fields on the live citizen form. It never
// mutates the input — the real spec (what gets saved) keeps its conditionals.

/** A copy of `spec` with all conditional visibility removed. */
export function revealAllComponents(spec: FormType): FormType {
  const clone = structuredClone(spec);
  stripConditionalVisibility(clone);
  return clone;
}

function stripConditionalVisibility(node: unknown): void {
  if (Array.isArray(node)) {
    node.forEach(stripConditionalVisibility);
    return;
  }
  if (node === null || typeof node !== "object") return;

  const record = node as Record<string, unknown>;
  delete record.conditional;
  delete record.customConditional;
  if (record.hidden === true) record.hidden = false;

  Object.values(record).forEach(stripConditionalVisibility);
}
