import { describe, expect, it } from "vitest";
import type { FormType } from "@formio/react/lib/components/Form";

import {
  describeChannelRules,
  dropUnsetChannelRules,
  flattenComponents,
  formLayoutOf,
  listEditableComponents,
  setFormLayout,
  setLabel,
  setRequired,
} from "./specEdit";

// The nested cases matter: the estimator spec nests fields inside panels and
// columns, and the no-mutation guarantee is what lets the live preview update.

function asSpec(components: unknown[]): FormType {
  return { display: "form", components } as unknown as FormType;
}

function componentsOf(spec: FormType): Record<string, unknown>[] {
  return (spec as unknown as { components: Record<string, unknown>[] })
    .components;
}

describe("listEditableComponents", () => {
  it("returns input fields with label and required, skipping layout/display and buttons", () => {
    const spec = asSpec([
      {
        key: "fullName",
        type: "textfield",
        label: "Full name",
        input: true,
        validate: { required: true },
      },
      { key: "help", type: "bcgovAccordion", input: false },
      { key: "panel", type: "panel", input: false },
      // Buttons and hidden inputs carry input:true but are not editable fields.
      { key: "submit", type: "button", input: true },
      { key: "trackingId", type: "hidden", input: true },
    ]);

    const editable = listEditableComponents(spec);

    expect(editable).toEqual([
      {
        key: "fullName",
        label: "Full name",
        required: true,
        type: "textfield",
      },
    ]);
  });

  it("finds fields nested in panels and columns", () => {
    const spec = asSpec([
      {
        type: "panel",
        input: false,
        components: [
          {
            type: "columns",
            input: false,
            columns: [
              {
                components: [
                  {
                    key: "nested",
                    type: "number",
                    label: "Income",
                    input: true,
                  },
                ],
              },
            ],
          },
        ],
      },
    ]);

    const editable = listEditableComponents(spec);

    expect(editable.map((c) => c.key)).toEqual(["nested"]);
    expect(editable[0].required).toBe(false);
  });
});

describe("flattenComponents", () => {
  it("lists every field in document order, lifting them out of containers", () => {
    const spec = asSpec([
      { key: "top", type: "textfield", input: true },
      {
        type: "panel",
        input: false,
        components: [
          { key: "inPanel", type: "number", input: true },
          {
            type: "columns",
            input: false,
            columns: [
              { components: [{ key: "inColumn", type: "radio", input: true }] },
            ],
          },
        ],
      },
      { key: "help", type: "bcgovAccordion", input: false },
    ]);

    const keys = flattenComponents(spec).map((c) => c.key);

    // Container shells (panel, columns) dropped; their children and the
    // display accordion kept, in order.
    expect(keys).toEqual(["top", "inPanel", "inColumn", "help"]);
  });
});

describe("setLabel", () => {
  it("changes the target label and leaves the input untouched", () => {
    const spec = asSpec([
      { key: "fullName", type: "textfield", label: "Full name", input: true },
    ]);

    const next = setLabel(spec, "fullName", "Your legal name");

    expect(componentsOf(next)[0].label).toBe("Your legal name");
    expect(componentsOf(spec)[0].label).toBe("Full name");
  });
});

describe("setRequired", () => {
  it("sets required, creating validate when absent, without mutating the input", () => {
    const spec = asSpec([
      { key: "fullName", type: "textfield", label: "Full name", input: true },
    ]);

    const next = setRequired(spec, "fullName", true);

    expect(
      (componentsOf(next)[0].validate as { required: boolean }).required,
    ).toBe(true);
    expect(componentsOf(spec)[0].validate).toBeUndefined();
  });

  it("clears required on a field that had it", () => {
    const spec = asSpec([
      {
        key: "fullName",
        type: "textfield",
        input: true,
        validate: { required: true },
      },
    ]);

    const next = setRequired(spec, "fullName", false);

    expect(
      (componentsOf(next)[0].validate as { required: boolean }).required,
    ).toBe(false);
  });
});

describe("setFormLayout", () => {
  const field = { key: "name", type: "textfield", label: "Name", input: true };
  const submit = {
    key: "submit",
    type: "button",
    action: "submit",
    label: "Submit",
    input: true,
  };

  it("wraps a one-page form in a first page and drops its Submit", () => {
    const spec = asSpec([field, submit]);

    const wizard = setFormLayout(spec, "wizard");

    expect(formLayoutOf(wizard)).toBe("wizard");
    expect(componentsOf(wizard)).toEqual([
      {
        type: "panel",
        key: "page1",
        title: "Page 1",
        label: "Page 1",
        input: false,
        components: [field],
      },
    ]);
    expect(componentsOf(spec)).toEqual([field, submit]);
  });

  it("keeps existing top-level panels as the pages", () => {
    const pages = [
      { key: "a", type: "panel", title: "A", components: [field] },
      { key: "b", type: "panel", title: "B", components: [] },
    ];

    expect(
      componentsOf(setFormLayout(asSpec([...pages, submit]), "wizard")),
    ).toEqual(pages);
  });

  it("does not reuse a key the form already has for the new page", () => {
    const spec = asSpec([{ ...field, key: "page1" }]);

    expect(componentsOf(setFormLayout(spec, "wizard"))[0].key).toBe("page12");
  });

  it("gives a one-page form its Submit back, keeping the pages as sections", () => {
    const wizard = {
      display: "wizard",
      components: [{ key: "a", type: "panel", components: [field] }],
    } as unknown as FormType;

    const form = setFormLayout(wizard, "form");

    expect(formLayoutOf(form)).toBe("form");
    expect(componentsOf(form).map((c) => c.type)).toEqual(["panel", "button"]);
  });

  it("adds no second Submit to a form that has one", () => {
    expect(
      componentsOf(setFormLayout(asSpec([field, submit]), "form")),
    ).toEqual([field, submit]);
  });
});

describe("a repeat section in the row-by-row editor", () => {
  const spec = {
    components: [
      { type: "number", key: "childCount", label: "How many?", input: true },
      {
        type: "bcgovRepeatSection",
        key: "children",
        label: "Children",
        input: true,
        components: [
          { type: "textfield", key: "childFirstName", label: "First name", input: true },
        ],
      },
    ],
  } as unknown as FormType;

  it("lists the section's questions, not the section", () => {
    expect(flattenComponents(spec).map((c) => c.key)).toEqual(["childCount", "childFirstName"]);
    expect(listEditableComponents(spec).map((c) => c.key)).toEqual([
      "childCount",
      "childFirstName",
    ]);
  });
});

describe("describeChannelRules", () => {
  function sinWith(myssChannels: unknown): Map<string, string[]> {
    return describeChannelRules(
      asSpec([{ type: "textfield", key: "sin", properties: { myssChannels } }]),
    );
  }

  it("names what each channel's rule does, in channel order", () => {
    expect(
      sinWith({ "worker-override": { required: false }, client: { show: false } }).get("sin"),
    ).toEqual(["Hidden for Client", "Optional for Worker override"]);
    expect(sinWith({ "worker-override": { required: true } }).get("sin")).toEqual([
      "Required for Worker override",
    ]);
  });

  it("says only Hidden for a channel that hides the field, whatever it requires", () => {
    expect(sinWith({ client: { show: false, required: true } }).get("sin")).toEqual([
      "Hidden for Client",
    ]);
  });

  it("says nothing for a field without rules, or with settings that are not true or false", () => {
    expect(describeChannelRules(asSpec([{ type: "textfield", key: "sin" }])).size).toBe(0);
    expect(sinWith({ client: { show: true } }).size).toBe(0);
    expect(sinWith({ client: { required: "no" } }).size).toBe(0);
  });

  it("says a field inside a hidden page, panel or column is hidden with it, naming the outermost", () => {
    const notes = describeChannelRules({
      display: "wizard",
      components: [
        {
          type: "panel",
          key: "page2",
          title: "Page 2",
          properties: { myssChannels: { "worker-override": { show: false } } },
          components: [
            {
              type: "textfield",
              key: "clientNote",
              properties: { myssChannels: { client: { required: true } } },
            },
            {
              type: "columns",
              key: "row",
              properties: { myssChannels: { "worker-override": { show: false } } },
              columns: [{ components: [{ type: "textfield", key: "inColumn" }] }],
            },
          ],
        },
      ],
    });

    expect(notes.get("page2")).toEqual(["Hidden for Worker override"]);
    expect(notes.get("clientNote")).toEqual([
      "Required for Client",
      'Hidden for Worker override (inside "Page 2")',
    ]);
    expect(notes.get("inColumn")).toEqual(['Hidden for Worker override (inside "Page 2")']);
  });

  it("says nothing of a count question's required rules, which have no effect", () => {
    const notes = describeChannelRules(
      asSpec([
        {
          type: "number",
          key: "childCount",
          properties: {
            myssChannels: { client: { required: false }, "worker-override": { show: false } },
          },
        },
        { type: "bcgovRepeatSection", key: "children", properties: { myssRepeatCount: "childCount" } },
      ]),
    );

    expect(notes.get("childCount")).toEqual(["Hidden for Worker override"]);
  });
});

describe("dropUnsetChannelRules", () => {
  const spec = asSpec([
    {
      type: "panel",
      key: "page1",
      input: false,
      components: [
        {
          type: "textfield",
          key: "sin",
          properties: {
            myssValidator: "sin",
            myssChannels: {
              client: { show: "", required: "" },
              "worker-override": { show: "", required: false },
            },
          },
        },
        {
          type: "textfield",
          key: "fullName",
          properties: {
            myssValidator: "",
            myssChannels: {
              client: { show: "", required: "" },
              "worker-override": { show: "", required: "" },
            },
          },
        },
      ],
    },
  ]);

  function propertiesOf(result: FormType, key: string): unknown {
    return flattenComponents(result).find((c) => c.key === key)?.properties;
  }

  it("keeps only the settings that were chosen, inside containers too", () => {
    expect(propertiesOf(dropUnsetChannelRules(spec), "sin")).toEqual({
      myssValidator: "sin",
      myssChannels: { "worker-override": { required: false } },
    });
  });

  it("drops the rules of a field left as authored, and nothing else of its properties", () => {
    expect(propertiesOf(dropUnsetChannelRules(spec), "fullName")).toEqual({ myssValidator: "" });
  });

  it("drops properties left empty once the rules are gone", () => {
    const panel = asSpec([
      {
        type: "panel",
        key: "page1",
        properties: { myssChannels: { client: { show: "" }, "worker-override": { show: "" } } },
        components: [],
      },
    ]);
    expect(componentsOf(dropUnsetChannelRules(panel))[0]).not.toHaveProperty("properties");
  });

  it("leaves any other wrong value for the rules to report", () => {
    const wrong = asSpec([
      { type: "textfield", key: "sin", properties: { myssChannels: { client: { show: "no" } } } },
      { type: "textfield", key: "a", properties: { myssChannels: { "worker-overide": { show: "" } } } },
      { type: "textfield", key: "b", properties: { myssChannels: { client: { visible: "" } } } },
    ]);
    expect(dropUnsetChannelRules(wrong)).toEqual(wrong);
  });

  it("does not mutate the spec it is given", () => {
    const before = structuredClone(spec);
    dropUnsetChannelRules(spec);
    expect(spec).toEqual(before);
  });
});
