import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import "@formio/js/dist/formio.form.min.css";
import { registerBcgovComponents } from "./bcgovComponents";

// SPIKE (temporary): does a display: "wizard" spec work with our renderer?

registerBcgovComponents();

const spec = {
  display: "wizard",
  components: [
    {
      type: "panel",
      key: "page1",
      title: "About you",
      input: false,
      components: [
        {
          type: "textfield",
          key: "firstName",
          label: "First name",
          input: true,
          validate: { required: true },
        },
        {
          type: "radio",
          key: "hasPartner",
          label: "Do you have a partner?",
          input: true,
          validate: { required: true },
          values: [
            { label: "Yes", value: "yes" },
            { label: "No", value: "no" },
          ],
        },
      ],
    },
    {
      type: "panel",
      key: "page2",
      title: "Your partner",
      input: false,
      conditional: { show: true, when: "hasPartner", eq: "yes" },
      components: [
        {
          type: "textfield",
          key: "partnerName",
          label: "Partner name",
          input: true,
          validate: { required: true },
        },
      ],
    },
    {
      type: "panel",
      key: "page3",
      title: "Contact",
      input: false,
      components: [
        {
          type: "email",
          key: "email",
          label: "Email",
          input: true,
          validate: { required: true },
        },
      ],
    },
  ],
} as unknown as FormType;

describe("wizard spike", () => {
  it("pages forward with validation, back with answers kept, and submits all pages", async () => {
    const submissions: Record<string, unknown>[] = [];
    const submitErrors: unknown[] = [];
    const screen = await render(
      <Form
        src={spec}
        options={{ noAlerts: true }}
        onSubmit={(s: { data: Record<string, unknown> }) => submissions.push(s.data)}
        onSubmitError={(e: unknown) => submitErrors.push(e)}
      />,
    );

    const firstName = screen.getByRole("textbox", { name: "First name" });
    await expect.element(firstName).toBeVisible();
    // Design system field inside a wizard page.
    expect(firstName.element().closest(".bcds-react-aria-TextField")).not.toBeNull();
    expect(screen.getByRole("textbox", { name: "Email" }).query()).toBeNull();

    // Next is blocked while page 1 is invalid.
    await screen.getByRole("button", { name: "Next" }).click();
    await expect.element(screen.getByRole("textbox", { name: "First name" })).toBeVisible();
    console.log("SPIKE submitError on blocked Next:", submitErrors.length);

    await userEvent.fill(firstName, "Ada");
    await screen.getByRole("radio", { name: "Yes" }).click();
    await screen.getByRole("button", { name: "Next" }).click();
    await expect.element(screen.getByRole("textbox", { name: "Partner name" })).toBeVisible();

    // Back to page 1: answer kept.
    await screen.getByRole("button", { name: "Previous" }).click();
    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toHaveValue("Ada");

    // Change answer so the partner page is skipped.
    await screen.getByRole("radio", { name: "No" }).click();
    await screen.getByRole("button", { name: "Next" }).click();
    await expect.element(screen.getByRole("textbox", { name: "Email" })).toBeVisible();

    await userEvent.fill(screen.getByRole("textbox", { name: "Email" }), "ada@example.com");
    await screen.getByRole("button", { name: "Submit" }).click();
    await expect.poll(() => submissions.length).toBe(1);
    console.log("SPIKE submitted data:", JSON.stringify(submissions[0]));
    console.log(
      "SPIKE nav markup classes:",
      document.querySelector(".formio-wizard-nav-container")?.outerHTML.slice(0, 300),
    );
  });
});
