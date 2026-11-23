import { describe, expect, it } from "vitest";
import type { FormType } from "@formio/react/lib/components/Form";

import {
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
