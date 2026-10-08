import type { FormValidationError } from "@/api/forms";

// Form.io's validation errors in the API's error shape, so a submit the
// browser blocked and a submit the API refused feed the same error summary.
// Form.io hands `submitError` an array of its error objects; the shape is loose
// (it differs between its own rules, the wrappers' registry rules and server
// errors it is re-showing), so every field is read defensively.

interface FormioErrorLike {
  message?: unknown;
  path?: unknown;
  ruleName?: unknown;
  keyword?: unknown;
  fromServer?: unknown;
  component?: { key?: unknown } | null;
  context?: { path?: unknown } | null;
}

function fieldOf(error: FormioErrorLike): string | undefined {
  for (const candidate of [
    error.path,
    error.context?.path,
    error.component?.key,
  ]) {
    if (typeof candidate === "string" && candidate !== "") return candidate;
  }
  return undefined;
}

function keywordOf(error: FormioErrorLike): string {
  if (typeof error.keyword === "string" && error.keyword !== "") {
    return error.keyword;
  }
  const rule =
    typeof error.ruleName === "string" && error.ruleName !== ""
      ? error.ruleName
      : "invalid";
  // Form.io's own rules have no catalogue keyword; the rule name under a
  // client prefix keeps the summary keyed the way the API's errors are.
  return `FORM.CLIENT.${rule.replace(/[^A-Za-z0-9]+/g, "_").toUpperCase()}`;
}

/**
 * The field errors in Form.io's `submitError` payload as the API's
 * `{ field, keyword, message }` list, one entry per field and message,
 * server errors Form.io is re-showing left out.
 */
export function toValidationErrors(raw: unknown): FormValidationError[] {
  const list: unknown[] = Array.isArray(raw) ? raw : raw ? [raw] : [];
  const seen = new Set<string>();
  const errors: FormValidationError[] = [];

  for (const entry of list) {
    if (typeof entry !== "object" || entry === null) continue;
    const error = entry as FormioErrorLike;
    if (error.fromServer === true) continue;
    const field = fieldOf(error);
    const message =
      typeof error.message === "string" ? error.message.trim() : "";
    if (!field || !message) continue;

    const key = `${field}\u0000${message}`;
    if (seen.has(key)) continue;
    seen.add(key);
    errors.push({ field, keyword: keywordOf(error), message });
  }

  return errors;
}
