import type { FormType } from "@formio/react/lib/components/Form";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import ReadOnlySpecForm from "./ReadOnlySpecForm";

// A submitted application is drawn read-only over its answers, with every
// button taken out of the spec, nested ones included.
const spec = {
  display: "form",
  components: [
    { type: "textfield", key: "firstName", label: "First name", input: true },
    {
      type: "panel",
      key: "details",
      title: "Details",
      components: [
        { type: "button", key: "save", label: "Save", action: "submit" },
      ],
    },
    { type: "button", key: "submit", label: "Submit", action: "submit" },
  ],
} as unknown as FormType;

describe("ReadOnlySpecForm", () => {
  it("shows the answers in fields that cannot be changed", async () => {
    const screen = await render(
      <ReadOnlySpecForm spec={spec} answers={{ firstName: "Ada" }} />,
    );

    const field = screen.getByRole("textbox", { name: "First name" });
    await expect.element(field).toHaveValue("Ada");
    await expect.element(field).toBeDisabled();
  });

  it("draws no buttons, not even one nested in a panel", async () => {
    const screen = await render(
      <ReadOnlySpecForm spec={spec} answers={{ firstName: "Ada" }} />,
    );

    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toBeInTheDocument();
    expect(screen.getByRole("button").elements()).toHaveLength(0);
  });
});
