import { describe, expect, it } from "vitest";

import {
  confirmValue,
  currentVersionOf,
  describeSaveTarget,
  isConfirmed,
  saveTargetFor,
  type VersionState,
} from "./formVersions";

const published = (version: number): VersionState => ({
  version,
  isPublished: true,
});
const draft = (version: number): VersionState => ({
  version,
  isPublished: false,
});

describe("saveTargetFor", () => {
  it("is a new form when there are no versions", () => {
    expect(saveTargetFor(undefined)).toEqual({ kind: "new-form" });
    expect(saveTargetFor([])).toEqual({ kind: "new-form" });
  });

  it("is the next version when the latest version is published", () => {
    expect(saveTargetFor([published(7), published(8)])).toEqual({
      kind: "new-version",
      version: 9,
      liveVersion: 8,
    });
  });

  it("replaces the draft when the latest version is unpublished", () => {
    expect(saveTargetFor([published(8), draft(9)])).toEqual({
      kind: "replace-draft",
      version: 9,
      liveVersion: 8,
    });
  });

  it("replaces a first draft that was never published", () => {
    expect(saveTargetFor([draft(1)])).toEqual({
      kind: "replace-draft",
      version: 1,
      liveVersion: null,
    });
  });

  it("does not depend on the order of the list", () => {
    expect(saveTargetFor([draft(9), published(2), published(8)])).toEqual(
      saveTargetFor([published(2), published(8), draft(9)]),
    );
  });
});

describe("currentVersionOf", () => {
  it("is the latest published version when there is one", () => {
    expect(currentVersionOf([published(7), published(8), draft(9)])).toBe(8);
  });

  it("is the draft when the form has never been published", () => {
    expect(currentVersionOf([draft(1)])).toBe(1);
  });

  it("is null for a new form", () => {
    expect(currentVersionOf([])).toBeNull();
    expect(currentVersionOf(undefined)).toBeNull();
  });
});

describe("confirmValue and isConfirmed", () => {
  const newVersion = saveTargetFor([published(8)]);
  const replaceDraft = saveTargetFor([published(8), draft(9)]);

  it("round-trips each existing-form target", () => {
    expect(confirmValue(newVersion)).toBe("new-9");
    expect(confirmValue(replaceDraft)).toBe("replace-9");
    expect(isConfirmed(newVersion, "new-9")).toBe(true);
    expect(isConfirmed(replaceDraft, "replace-9")).toBe(true);
  });

  it("does not let one target's confirmation stand for another", () => {
    expect(isConfirmed(replaceDraft, "new-9")).toBe(false);
    expect(isConfirmed(newVersion, "new-10")).toBe(false);
    expect(isConfirmed(newVersion, null)).toBe(false);
  });

  it("gives a new form no confirmation", () => {
    expect(confirmValue({ kind: "new-form" })).toBeNull();
    expect(isConfirmed({ kind: "new-form" }, null)).toBe(false);
  });
});

describe("describeSaveTarget", () => {
  it("describes a new version", () => {
    expect(
      describeSaveTarget(
        { kind: "new-version", version: 9, liveVersion: 8 },
        "BC Bus Pass",
        "bc-bus-pass",
      ),
    ).toEqual({
      heading: 'Create a new version of "BC Bus Pass"?',
      body:
        "A form with the ID bc-bus-pass already exists. Your JSON will be saved as " +
        "version 9, as a draft. Version 8 stays live until you publish version 9.",
      confirmLabel: "Create version 9",
    });
  });

  it("describes replacing a draft, naming the live version", () => {
    expect(
      describeSaveTarget(
        { kind: "replace-draft", version: 9, liveVersion: 8 },
        "BC Bus Pass",
        "bc-bus-pass",
      ),
    ).toEqual({
      heading: 'Replace the unpublished draft of "BC Bus Pass"?',
      body:
        "Version 9 of bc-bus-pass is an unpublished draft. Saving your JSON will " +
        "replace that draft, and its current content will be lost. Version 8 stays " +
        "live until you publish.",
      confirmLabel: "Replace draft version 9",
    });
  });

  it("says when a replaced draft's form has never been published", () => {
    const warning = describeSaveTarget(
      { kind: "replace-draft", version: 1, liveVersion: null },
      "Child care intake",
      "child-care-intake",
    );
    expect(warning.body).toMatch(/This form has never been published\.$/);
  });
});
