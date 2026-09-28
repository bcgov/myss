import { describe, expect, it } from "vitest";

import { ALLOWED_COMPONENT_TYPES, builderOptions } from "./builderOptions";

// The palette's shape. That each listed type is actually registered with
// Form.io — and that the key field really disappears from the settings dialog —
// needs the Form.io runtime, so those live in FormEditorPage.browser.test.tsx.

/** One override entry as the builder's editForm option carries them. */
interface TabOverride {
  key: string;
  ignore?: boolean;
  components?: {
    key: string;
    ignore?: boolean;
    disabled?: boolean;
    type?: string;
  }[];
}

function overridesFor(type: string): TabOverride[] {
  return builderOptions.editForm[type] as TabOverride[];
}

describe("builderOptions", () => {
  it("turns off every one of Form.io's own palette groups", () => {
    const { basic, advanced, layout, data, premium } = builderOptions.builder;
    expect([basic, advanced, layout, data, premium]).toEqual([
      false,
      false,
      false,
      false,
      false,
    ]);
  });

  it("offers exactly the allowed types across its three groups", () => {
    const { choices, text, content } = builderOptions.builder;
    const offered = [choices, text, content].flatMap((group) =>
      Object.keys(group.components),
    );

    expect(offered.slice().sort()).toEqual(
      ALLOWED_COMPONENT_TYPES.slice().sort(),
    );
    // No type is offered twice, which would show it in both groups.
    expect(new Set(offered).size).toBe(offered.length);
  });

  it("does not offer types the citizen renderer has no treatment for", () => {
    for (const type of [
      "survey",
      "signature",
      "file",
      "datagrid",
      "editgrid",
      "address",
    ]) {
      expect(ALLOWED_COMPONENT_TYPES).not.toContain(type);
    }
  });

  it("offers a button, so a deleted submit button can be put back", () => {
    expect(ALLOWED_COMPONENT_TYPES).toContain("button");
  });

  it("locks the component key for every allowed type", () => {
    for (const type of ALLOWED_COMPONENT_TYPES) {
      const apiTab = overridesFor(type).find((tab) => tab.key === "api");
      // Disabled, not removed: the builder writes the derived key into it.
      expect(apiTab?.components).toEqual([{ key: "key", disabled: true }]);
    }
  });

  it("removes multiple values from the question types only", () => {
    const questions = ALLOWED_COMPONENT_TYPES.filter(
      (type) => !["bcgovAccordion", "content", "panel", "button"].includes(type),
    );
    for (const type of questions) {
      const dataTab = overridesFor(type).find((tab) => tab.key === "data");
      expect(dataTab?.components, type).toEqual([
        { key: "multiple", ignore: true },
      ]);
    }
    // The others have no Data tab; an override would invent an empty one.
    for (const type of ["bcgovAccordion", "content", "panel", "button"]) {
      expect(
        overridesFor(type).find((tab) => tab.key === "data"),
        type,
      ).toBeUndefined();
    }
  });

  it("gives the accordion its own heading and body fields instead of a label", () => {
    const displayTab = overridesFor("bcgovAccordion").find(
      (tab) => tab.key === "display",
    );
    const fields = displayTab?.components ?? [];

    expect(fields.find((f) => f.key === "label")?.ignore).toBe(true);
    expect(fields.find((f) => f.key === "accordionLabel")?.type).toBe(
      "textfield",
    );
    expect(fields.find((f) => f.key === "accordionBody")?.type).toBe(
      "textarea",
    );
  });

  it("does not add a submit button of its own", () => {
    // The seeded specs carry their own submit button; a second one would post
    // the form twice over.
    expect(builderOptions.noDefaultSubmitButton).toBe(true);
  });
});
