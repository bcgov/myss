// The error message catalogue as the Form.io wrappers see it: a module-level
// copy of what /v1/forms/error-messages served, filled by
// useErrorMessageCatalogue (mounted once in App). Form.io components are not
// React components and cannot call a hook, so the wording reaches them this
// way. Until the catalogue loads, or when it cannot, lookups return undefined
// and the rule's compiled wording is used instead.

import type { ErrorMessageCatalogue } from "@/api/errorMessages";

let catalogue: ErrorMessageCatalogue = {};

/** Replaces the catalogue the wrappers read. Idempotent. */
export function setErrorCatalogue(
  next: ErrorMessageCatalogue | undefined,
): void {
  catalogue = next ?? {};
}

/** The catalogue's wording for a keyword, or undefined when it has no row. */
export function catalogueMessage(keyword: string): string | undefined {
  const message = catalogue[keyword];
  return typeof message === "string" && message.trim() !== ""
    ? message
    : undefined;
}
