import { describe, expect, it } from "vitest";

import {
  ALLOWED_COMPONENT_TYPES,
  builderOptions,
  countQuestionOptions,
  isRepeatCountQuestion,
} from "./builderOptions";
import { CHANNEL_RULES_SCOPE } from "./requestChannels";

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
      expect.arrayContaining(["addAnother", "reorder", "initEmpty", "disableAddingRemovingRows", "hidden"]),
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

describe("the Channel rules tab", () => {
  interface ChannelChoice {
    key: string;
    dataType?: string;
    placeholder?: string;
    data?: { values: { label: string; value: string }[] };
    customConditional?: (context: unknown) => boolean;
  }

  interface ChannelFieldset {
    key?: string;
    content?: string;
    legend?: string;
    components?: ChannelChoice[];
  }

  interface ChannelTab {
    key: string;
    label?: string;
    components: ChannelFieldset[];
  }

  function channelTab(type: string): ChannelTab | undefined {
    return (builderOptions.editForm[type] as ChannelTab[]).find((tab) => tab.key === "channelRules");
  }

  function choices(type: string): ChannelChoice[] {
    return (channelTab(type)?.components ?? []).flatMap((node) => node.components ?? []);
  }

  const OLDER_QUESTION_TYPES = ["bcgovRadio", "phoneNumber", "bcgovAddressAutocomplete"];
  const NO_ANSWER_TYPES = [
    "bcgovAccordion",
    "bcgovCallout",
    "bcgovInlineAlert",
    "content",
    "panel",
    "button",
    "bcgovRepeatSection",
    "htmlelement",
  ];

  it("is on every allowed type and on the older question types", () => {
    for (const type of [...ALLOWED_COMPONENT_TYPES, ...OLDER_QUESTION_TYPES, "htmlelement"]) {
      expect(channelTab(type)?.label, type).toBe("Channel rules");
    }
  });

  it("says in its help that only applications for assistance use channel rules", () => {
    for (const type of ["textfield", "content"]) {
      const help = channelTab(type)?.components.find((node) => node.key === "channelRulesHelp");
      expect(help?.content, type).toContain(CHANNEL_RULES_SCOPE);
    }
  });

  it("asks of a question, for each channel, whether it is hidden and whether it is required", () => {
    const questions = [
      ...ALLOWED_COMPONENT_TYPES.filter((type) => !NO_ANSWER_TYPES.includes(type)),
      ...OLDER_QUESTION_TYPES,
    ];
    for (const type of questions) {
      expect(choices(type).map((choice) => choice.key), type).toEqual([
        "properties.myssChannels.client.show",
        "properties.myssChannels.client.required",
        "properties.myssChannels.worker-override.show",
        "properties.myssChannels.worker-override.required",
      ]);
    }
    expect(channelTab("textfield")?.components.map((node) => node.legend).filter(Boolean)).toEqual([
      "Client",
      "Worker override",
    ]);
  });

  it("asks only whether a component that takes no answer is hidden", () => {
    for (const type of NO_ANSWER_TYPES) {
      expect(choices(type).map((choice) => choice.key), type).toEqual([
        "properties.myssChannels.client.show",
        "properties.myssChannels.worker-override.show",
      ]);
    }
  });

  it("stores a choice as true or false, and reads As authored while none is made", () => {
    const [show, required] = choices("textfield");

    expect(show.dataType).toBe("boolean");
    expect(show.placeholder).toBe("As authored");
    expect(show.data?.values).toEqual([{ label: "Hidden", value: "false" }]);
    expect(required.dataType).toBe("boolean");
    expect(required.placeholder).toBe("As authored");
    expect(required.data?.values).toEqual([
      { label: "Required", value: "true" },
      { label: "Optional", value: "false" },
    ]);
  });

  it("leaves out the Required choice on a repeat section's count question", () => {
    const spec = {
      components: [
        { type: "number", key: "childCount" },
        { type: "number", key: "petCount" },
        { type: "bcgovRepeatSection", key: "children", properties: { myssRepeatCount: "childCount" } },
      ],
    };
    const required = choices("number").find((choice) => choice.key.endsWith(".required"));
    const shown = (key: string) =>
      required?.customConditional?.({ instance: { options: { editForm: spec, editComponent: { key } } } });

    expect(shown("childCount")).toBe(false);
    expect(shown("petCount")).toBe(true);
    expect(required?.customConditional?.({})).toBe(true);
  });
});

describe("isRepeatCountQuestion", () => {
  const spec = {
    components: [
      { type: "number", key: "childCount" },
      {
        type: "panel",
        key: "page",
        components: [
          { type: "bcgovRepeatSection", key: "children", properties: { myssRepeatCount: "childCount" } },
        ],
      },
    ],
  };

  it("is true for the question a repeat section counts with, wherever the section is", () => {
    expect(isRepeatCountQuestion(spec, "childCount")).toBe(true);
  });

  it("is false for any other key, a missing key and a form without a section", () => {
    expect(isRepeatCountQuestion(spec, "page")).toBe(false);
    expect(isRepeatCountQuestion(spec, undefined)).toBe(false);
    expect(isRepeatCountQuestion({ components: [{ type: "number", key: "childCount" }] }, "childCount")).toBe(false);
  });
});
