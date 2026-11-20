import { describe, expect, it } from "vitest";

import {
  ALLOWED_COMPONENT_TYPES,
  builderOptions,
  countQuestionOptions,
} from "./builderOptions";

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
    const CONTENT = [
      "bcgovAccordion",
      "bcgovCallout",
      "bcgovInlineAlert",
      "content",
      "panel",
      "button",
    ];
    // The repeat section holds questions and has Data tab settings of its own.
    const questions = ALLOWED_COMPONENT_TYPES.filter(
      (type) => !CONTENT.includes(type) && type !== "bcgovRepeatSection",
    );
    for (const type of questions) {
      const dataTab = overridesFor(type).find((tab) => tab.key === "data");
      expect(dataTab?.components, type).toEqual([
        { key: "multiple", ignore: true },
      ]);
    }
    // The others have no Data tab; an override would invent an empty one.
    for (const type of CONTENT) {
      expect(
        overridesFor(type).find((tab) => tab.key === "data"),
        type,
      ).toBeUndefined();
    }
  });

  it("gives the callout a heading, heading level and colour instead of a legend", () => {
    const displayTab = overridesFor("bcgovCallout").find(
      (tab) => tab.key === "display",
    );
    const fields = displayTab?.components ?? [];

    expect(fields.find((f) => f.key === "legend")?.ignore).toBe(true);
    expect(fields.map((f) => f.key)).toEqual(
      expect.arrayContaining(["calloutTitle", "headingLevel", "variant"]),
    );
    expect(ALLOWED_COMPONENT_TYPES).toContain("bcgovCallout");
  });

  it("offers amounts (currency) beside numbers", () => {
    expect(ALLOWED_COMPONENT_TYPES).toEqual(
      expect.arrayContaining(["number", "currency"]),
    );
    expect(
      overridesFor("currency").find((tab) => tab.key === "data")?.components,
    ).toEqual([{ key: "multiple", ignore: true }]);
  });

  it("lets a checkbox carry a question above it", () => {
    const displayTab = overridesFor("checkbox").find(
      (tab) => tab.key === "display",
    );
    expect(displayTab?.components?.map((f) => f.key)).toEqual([
      "properties.myssGroupLabel",
    ]);
  });

  it("gives the inline alert a title and kind instead of a legend", () => {
    const displayTab = overridesFor("bcgovInlineAlert").find(
      (tab) => tab.key === "display",
    );
    const fields = displayTab?.components ?? [];

    expect(fields.find((f) => f.key === "legend")?.ignore).toBe(true);
    expect(fields.map((f) => f.key)).toEqual(
      expect.arrayContaining(["alertTitle", "variant"]),
    );
    expect(ALLOWED_COMPONENT_TYPES).toContain("bcgovInlineAlert");
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

describe("the repeat section in the builder", () => {
  function tab(key: string) {
    return overridesFor("bcgovRepeatSection").find((t) => t.key === key)
      ?.components;
  }

  it("is offered beside the content and layout types", () => {
    expect(ALLOWED_COMPONENT_TYPES).toContain("bcgovRepeatSection");
    expect(Object.keys(builderOptions.builder.content.components)).toContain(
      "bcgovRepeatSection",
    );
  });

  it("asks for each entry's heading, the count question and the most entries", () => {
    const fields = tab("display") ?? [];
    const asked = fields.filter((f) => !f.ignore);

    expect(asked.map((f) => f.key)).toEqual([
      "properties.myssRowLabel",
      "properties.myssRepeatCount",
      "properties.myssRepeatMax",
    ]);
    expect(fields.find((f) => f.key === "properties.myssRepeatCount")?.type).toBe(
      "select",
    );
  });

  it("drops the data grid's row controls, default rows and row limits", () => {
    const ignored = (key: string) =>
      (tab(key) ?? []).filter((f) => f.ignore).map((f) => f.key);

    expect(ignored("display")).toEqual(
      expect.arrayContaining(["addAnother", "reorder", "initEmpty", "disableAddingRemovingRows"]),
    );
    expect(ignored("data")).toEqual(["defaultValue"]);
    expect(ignored("validation")).toEqual(
      expect.arrayContaining([
        "validate.required",
        "validate.minLength",
        "validate.maxLength",
        "custom-validation-js",
        "json-validation-json",
      ]),
    );
  });
});

describe("countQuestionOptions", () => {
  const spec = {
    components: [
      { type: "number", key: "childCount", label: "How many children?" },
      { type: "textfield", key: "firstName", label: "First name" },
      {
        type: "panel",
        key: "more",
        components: [{ type: "number", key: "petCount", label: "" }],
      },
      {
        type: "bcgovRepeatSection",
        key: "children",
        components: [{ type: "number", key: "childAge", label: "Age" }],
      },
    ],
  };

  it("offers the number questions outside every repeat section, by label and key", () => {
    expect(countQuestionOptions(spec)).toEqual([
      { label: "How many children? (childCount)", value: "childCount" },
      { label: "petCount (petCount)", value: "petCount" },
    ]);
  });

  it("offers nothing for a form without one", () => {
    expect(countQuestionOptions(undefined)).toEqual([]);
    expect(countQuestionOptions({ components: [{ type: "textfield", key: "a" }] })).toEqual([]);
  });

  it("is what the count question's list is built from", () => {
    const count = (overridesFor("bcgovRepeatSection")
      .find((t) => t.key === "display")
      ?.components ?? []).find((f) => f.key === "properties.myssRepeatCount") as unknown as {
      data: { custom(context: unknown): unknown };
    };

    expect(count.data.custom({ instance: { options: { editForm: spec } } })).toEqual(
      countQuestionOptions(spec),
    );
  });
});
