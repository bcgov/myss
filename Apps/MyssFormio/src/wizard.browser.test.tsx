import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import "@formio/js/dist/formio.form.min.css";
import { registerBcgovComponents } from "./bcgovComponents";

// A `display: "wizard"` spec as a citizen meets it: one top-level panel per
// page, drawn with the design system fields, paged by Form.io's own buttons.

registerBcgovComponents();

const spec = {
  display: "wizard",
  components: [
    {
      type: "panel",
      key: "aboutYou",
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
      key: "partner",
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
      key: "contact",
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

describe("wizard form", () => {
  it("pages forward with validation, back with answers kept, skips a hidden page and submits every page", async () => {
    const submissions: Record<string, unknown>[] = [];
    const screen = await render(
      <Form
        src={spec}
        options={{ noAlerts: true }}
        onSubmit={(submission: { data: Record<string, unknown> }) =>
          submissions.push(submission.data)
        }
      />,
    );
    // The design system covers each radio input with its label.
    const choose = (label: string) =>
      screen.getByText(label, { exact: true }).click();
    // Form.io settles the page list just after a change; Next before that
    // still pages through the old list.
    const partnerPage = screen.getByText("Your partner", { exact: true });

    const firstName = screen.getByRole("textbox", { name: "First name" });
    await expect.element(firstName).toBeVisible();
    expect(
      firstName.element().closest(".bcds-react-aria-TextField"),
    ).not.toBeNull();
    expect(screen.getByRole("textbox", { name: "Email" }).query()).toBeNull();

    // Next is refused while the page has an unanswered required question.
    await screen.getByRole("button", { name: "Next" }).click();
    await expect.element(firstName).toBeVisible();
    expect(screen.getByRole("textbox", { name: "Email" }).query()).toBeNull();

    await userEvent.fill(firstName, "Ada");
    await choose("Yes");
    await expect.element(partnerPage).toBeVisible();
    await screen.getByRole("button", { name: "Next" }).click();
    await expect
      .element(screen.getByRole("textbox", { name: "Partner name" }))
      .toBeVisible();

    await screen.getByRole("button", { name: "Previous" }).click();
    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toHaveValue("Ada");

    // "No" hides the partner page, so Next goes straight to Contact.
    await choose("No");
    await expect.element(partnerPage).not.toBeInTheDocument();
    await screen.getByRole("button", { name: "Next" }).click();
    const email = screen.getByRole("textbox", { name: "Email" });
    await expect.element(email).toBeVisible();

    await userEvent.fill(email, "ada@example.com");
    await screen.getByRole("button", { name: "Submit Form" }).click();

    await expect.poll(() => submissions.length).toBe(1);
    expect(submissions[0]).toMatchObject({
      firstName: "Ada",
      hasPartner: "no",
      email: "ada@example.com",
    });
    expect(submissions[0].partnerName ?? "").toBe("");
  });
});
