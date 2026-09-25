import { describe, expect, it } from "vitest";
import type { FormType } from "@formio/react/lib/components/Form";

import {
  flattenComponents,
  listEditableComponents,
  setLabel,
  setRequired,
} from "@/widgets/formio/specEdit";

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
      { key: "fullName", label: "Full name", required: true, type: "textfield" },
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

    expect((componentsOf(next)[0].validate as { required: boolean }).required).toBe(
      true,
    );
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

    expect((componentsOf(next)[0].validate as { required: boolean }).required).toBe(
      false,
    );
  });
});
