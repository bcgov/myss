import { describe, expect, it } from "vitest";
import type { FormType } from "@formio/react/lib/components/Form";

import { revealAllComponents } from "@/formio/specPreview";

// The preview transform strips conditional visibility so every component shows.
// The nested case matters: the estimator spec hides fields inside panels and
// columns, not only at the top level.

type Comp = {
  key: string;
  type?: string;
  conditional?: unknown;
  customConditional?: unknown;
  hidden?: boolean;
  components?: Comp[];
};

function asSpec(components: Comp[]): FormType {
  return { display: "form", components } as unknown as FormType;
}

function componentsOf(spec: FormType): Comp[] {
  return (spec as unknown as { components: Comp[] }).components;
}

describe("revealAllComponents", () => {
  it("removes conditional visibility from top-level components", () => {
    const spec = asSpec([
      {
        key: "q2",
        type: "radio",
        conditional: { json: { var: "data.q1" } },
        hidden: true,
      },
    ]);

    const result = componentsOf(revealAllComponents(spec))[0];

    expect(result.conditional).toBeUndefined();
    expect(result.hidden).toBe(false);
    expect(result.key).toBe("q2");
  });

  it("recurses into nested components", () => {
    const spec = asSpec([
      {
        key: "panel",
        type: "panel",
        components: [
          {
            key: "nested",
            type: "textfield",
            customConditional: "show = data.q1 === 'yes'",
          },
        ],
      },
    ]);

    const nested = componentsOf(revealAllComponents(spec))[0].components![0];

    expect(nested.customConditional).toBeUndefined();
    expect(nested.key).toBe("nested");
  });

  it("does not mutate the input spec", () => {
    const spec = asSpec([
      { key: "q2", conditional: { json: { var: "data.q1" } }, hidden: true },
    ]);

    revealAllComponents(spec);

    const original = componentsOf(spec)[0];
    expect(original.conditional).toBeDefined();
    expect(original.hidden).toBe(true);
  });
});
