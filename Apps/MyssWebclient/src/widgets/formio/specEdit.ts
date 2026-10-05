import type { FormType } from "@formio/react/lib/components/Form";
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
// and hidden data carriers both set input:true but have no label to change.
const NON_EDITABLE_TYPES = new Set(["button", "hidden"]);

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
