// What a save of pasted JSON writes for a form ID, worked out from the form's
// versions as the forms list reports them, and the wording that asks an
// administrator to confirm it. Form management and the JSON page both decide
// here, so they always agree. No fetch, no React.

/** One version of a form and whether it is published (as in the forms list). */
export interface VersionState {
  readonly version: number;
  readonly isPublished: boolean;
}

/**
 * What the first save writes: version 1 of a new form, the next version of a
 * form whose latest version is published, or a replacement of the form's
 * unpublished draft. `liveVersion` is the latest published version.
 */
export type SaveTarget =
  | { readonly kind: "new-form" }
  | {
      readonly kind: "new-version";
      readonly version: number;
      readonly liveVersion: number;
    }
  | {
      readonly kind: "replace-draft";
      readonly version: number;
      readonly liveVersion: number | null;
    };

/** A save target for a form that already exists. */
export type ExistingFormTarget = Exclude<SaveTarget, { kind: "new-form" }>;

/** The words of the warning that asks to confirm an existing form's target. */
export interface SaveTargetWarning {
  readonly heading: string;
  readonly body: string;
  readonly confirmLabel: string;
}

function highest(versions: readonly VersionState[]): number | null {
  return versions.reduce<number | null>(
    (max, entry) => (max === null || entry.version > max ? entry.version : max),
    null,
  );
}

function highestPublished(versions: readonly VersionState[]): number | null {
  return highest(versions.filter((entry) => entry.isPublished));
}

/** The save target for a form with these versions; none means a new form. */
export function saveTargetFor(
  versions: readonly VersionState[] | undefined,
): SaveTarget {
  const latest = highest(versions ?? []);
  if (latest === null) return { kind: "new-form" };

  const liveVersion = highestPublished(versions ?? []);
  return liveVersion === latest
    ? { kind: "new-version", version: latest + 1, liveVersion }
    : { kind: "replace-draft", version: latest, liveVersion };
}

/**
 * The version pasted JSON is compared with: the latest published version, or
 * the draft when the form has never been published. Null for a new form.
 */
export function currentVersionOf(
  versions: readonly VersionState[] | undefined,
): number | null {
  return highestPublished(versions ?? []) ?? highest(versions ?? []);
}

/** The confirmation recorded in the JSON page's URL, e.g. "new-9"; null for a new form. */
export function confirmValue(target: SaveTarget): string | null {
  switch (target.kind) {
    case "new-form":
      return null;
    case "new-version":
      return `new-${target.version}`;
    case "replace-draft":
      return `replace-${target.version}`;
  }
}

/** True when `value` confirms exactly this target. A new form needs no confirmation. */
export function isConfirmed(
  target: SaveTarget,
  value: string | null | undefined,
): boolean {
  const expected = confirmValue(target);
  return expected !== null && expected === value;
}

/** The warning shown before pasted JSON is saved to an existing form. */
export function describeSaveTarget(
  target: ExistingFormTarget,
  formName: string,
  formSpecId: string,
): SaveTargetWarning {
  if (target.kind === "new-version") {
    return {
      heading: `Create a new version of "${formName}"?`,
      body:
        `A form with the ID ${formSpecId} already exists. Your JSON will be saved as ` +
        `version ${target.version}, as a draft. Version ${target.liveVersion} stays ` +
        `live until you publish version ${target.version}.`,
      confirmLabel: `Create version ${target.version}`,
    };
  }

  const live =
    target.liveVersion === null
      ? "This form has never been published."
      : `Version ${target.liveVersion} stays live until you publish.`;
  return {
    heading: `Replace the unpublished draft of "${formName}"?`,
    body:
      `Version ${target.version} of ${formSpecId} is an unpublished draft. Saving your ` +
      `JSON will replace that draft, and its current content will be lost. ${live}`,
    confirmLabel: `Replace draft version ${target.version}`,
  };
}
