// Mirrors the pattern MyssContent's form-spec schema enforces on formSpecId,
// so a bad ID is an inline hint here instead of a refused write later.
export const FORM_ID_PATTERN = /^[a-z0-9-]+$/;

/** True when `id` is a form ID the content engine accepts. */
export function isValidFormId(id: string): boolean {
  return FORM_ID_PATTERN.test(id);
}
