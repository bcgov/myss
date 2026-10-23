import type { FormType } from "@formio/react/lib/components/Form";
import {
  readValidationErrors,
  type FormSpecPayload,
  type FormValidationError,
} from "@myss/web-common/api";
import { authHeaders } from "@myss/web-common/auth";

import { API_URL } from "@/constants";

// The form editor's calls to MyssAdminApi (/v1/forms): list forms, read and
// save drafts, publish. All need an IDIR sign-in. Responses come wrapped in the
// API's payload envelope.
//
// The spec and validation-error shapes, and the 422 reader, are shared with
// the citizen webclient (@myss/web-common/api); re-exported here for this
// app's callers.

export {
  readValidationErrors,
  type FormSpecPayload,
  type FormValidationError,
};

/** One version of a form and whether it is published or a draft only. */
export interface FormVersionSummary {
  version: number;
  isPublished: boolean;
}

/** One logical form and its versions, for the admin editor's forms list. */
export interface FormSummary {
  formSpecId: string;
  title?: string | null;
  versions: FormVersionSummary[];
}

/** The edited spec and title sent when saving a draft. */
export interface SaveDraftInput {
  spec: FormType;
  title?: string | null;
  /** The version this save must create as a new draft; omit for an ordinary save. */
  newVersion?: number;
}

/** The result of publishing a form: the version number that went live. */
export interface PublishResult {
  version: number;
}

/**
 * A spec the admin write path refused, carrying every reason it gave.
 *
 * For the editor's save/publish calls: MyssAdminApi answers 422 with the FULL error collection (the structural
 * failures from `ValidateSpecStructure`, or a translated Strapi lifecycle
 * refusal), so the editor can build one WCAG error summary in a single pass.
 *
 * `errors` is empty for non-422 failures (401, 502, a proxy error page), where
 * there is no collection to parse — callers should handle that case rather than
 * assuming a non-empty list.
 */
export class SpecRejectedError extends Error {
  readonly status: number;
  readonly errors: readonly FormValidationError[];

  constructor(status: number, errors: readonly FormValidationError[]) {
    super(
      errors.length > 0
        ? errors.map((error) => error.message).join(" ")
        : `Spec rejected (${status})`,
    );
    this.name = "SpecRejectedError";
    this.status = status;
    this.errors = errors;

    // Subclassing a built-in loses the prototype link when TypeScript downlevels
    // the class, which silently breaks `instanceof`. Restore it explicitly so the
    // editor's `instanceof SpecRejectedError` check holds regardless of target.
    Object.setPrototypeOf(this, SpecRejectedError.prototype);
  }
}

/**
 * A read that failed, carrying the HTTP status so callers can tell a missing
 * form (404) apart from a genuine load error (401, 500, a proxy error page).
 */
export class FormLoadError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "FormLoadError";
    this.status = status;

    // Restore the prototype link lost when TypeScript downlevels the class, so
    // `instanceof FormLoadError` holds regardless of compile target.
    Object.setPrototypeOf(this, FormLoadError.prototype);
  }
}

// The endpoints behind MyssAdminApi's `AdminIdir` policy. A 409 or 422 from a
// write carries the full error collection, surfaced as a typed
// SpecRejectedError.

/** The keyword of a save refused because the version it would create is taken. */
export const VERSION_TAKEN = "FORMSPEC.VERSION.TAKEN";

/** True when a save was refused because the version it would create is taken. */
export function isVersionTaken(error: unknown): boolean {
  return (
    error instanceof SpecRejectedError &&
    error.errors.some(
      (validationError) => validationError.keyword === VERSION_TAKEN,
    )
  );
}

/** Every form with its versions and published/draft state (admin list). */
export async function listForms(): Promise<FormSummary[]> {
  const res = await fetch(`${API_URL}/v1/forms`, {
    headers: authHeaders(),
  });
  if (!res.ok) throw new Error(`Forms list failed (${res.status})`);
  return (await res.json()).payload;
}

/**
 * The spec to open in the editor: the in-progress draft, or the latest
 * published version as the starting point for a new draft.
 */
export async function getDraft(formSpecId: string): Promise<FormSpecPayload> {
  const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/draft`, {
    headers: authHeaders(),
  });
  if (!res.ok)
    throw new FormLoadError(res.status, `Draft fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/** One version of a form, published or draft. */
export async function getFormVersion(
  formSpecId: string,
  version: number,
): Promise<FormSpecPayload> {
  const res = await fetch(
    `${API_URL}/v1/forms/${encodeURIComponent(formSpecId)}/versions/${version}`,
    { headers: authHeaders() },
  );
  if (!res.ok)
    throw new FormLoadError(res.status, `Version fetch failed (${res.status})`);
  return (await res.json()).payload;
}

/**
 * Validates and stores an edited spec as a draft (not published). A refused
 * spec (422), or a version that is taken when `newVersion` is given (409),
 * throws {@link SpecRejectedError} carrying the field errors.
 */
export async function saveDraft(
  formSpecId: string,
  input: SaveDraftInput,
): Promise<FormSpecPayload> {
  const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/draft`, {
    method: "PUT",
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: JSON.stringify(input),
  });
  if (!res.ok)
    throw new SpecRejectedError(res.status, await readValidationErrors(res));
  return (await res.json()).payload;
}

/**
 * Publishes the current draft as the next version. A refused publish (422)
 * throws {@link SpecRejectedError} carrying the field errors.
 */
export async function publishForm(formSpecId: string): Promise<PublishResult> {
  const res = await fetch(`${API_URL}/v1/forms/${formSpecId}/publish`, {
    method: "POST",
    headers: authHeaders(),
  });
  if (!res.ok)
    throw new SpecRejectedError(res.status, await readValidationErrors(res));
  return (await res.json()).payload;
}
