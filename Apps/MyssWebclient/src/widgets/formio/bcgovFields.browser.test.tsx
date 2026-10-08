import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import "@formio/js/dist/formio.form.min.css";
import { setErrorCatalogue } from "@/lib/errorCatalogue";
import { registerBcgovComponents } from "./bcgovComponents";

// The design system wrappers for Form.io's stock types, as a citizen meets
// them: drawn by the design system, valued and validated by Form.io, with the
// registry rules and the shared message gate layered on by bcgovField.

registerBcgovComponents();

const spec = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "firstName",
      label: "First name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "sin",
      label: "SIN",
      input: true,
      properties: { myssValidator: "sin" },
      errors: { sin: "Enter the SIN from your card" },
    },
    {
      type: "textfield",
      key: "otherSin",
      label: "Other SIN",
      input: true,
      properties: { myssValidator: "sin" },
    },
    {
      type: "textfield",
      key: "phone",
      label: "Phone",
      input: true,
      inputMask: "(999) 999-9999",
    },
    {
      type: "number",
      key: "monthlyIncome",
      label: "Monthly income",
      input: true,
    },
    {
      type: "select",
      key: "phoneType",
      label: "Phone type",
      input: true,
      dataSrc: "values",
      data: {
        values: [
          { label: "Home", value: "home" },
          { label: "Cell", value: "cell" },
        ],
      },
    },
    {
      type: "checkbox",
      key: "agrees",
      label: "I agree",
      input: true,
      validate: { required: true, customMessage: "Agreement is required" },
    },
    {
      type: "checkbox",
      key: "consent",
      label:
        'I accept the <a href="https://example.gov.bc.ca/terms">Terms of Use</a> and <a href="https://example.gov.bc.ca/privacy">Privacy Statement</a>.',
      input: true,
    },
    {
      type: "email",
      key: "email",
      label: "Email",
      input: true,
    },
    {
      type: "email",
      key: "emailVerification",
      label: "Email verification",
      input: true,
      properties: { myssMatches: "email" },
    },
    {
      type: "textfield",
      key: "postal",
      label: "Postal",
      input: true,
      validate: { pattern: "^[A-Z][0-9][A-Z] ?[0-9][A-Z][0-9]$" },
      errors: { pattern: "Invalid postal code format" },
      validateOn: "blur",
    },
    {
      type: "datetime",
      key: "dateOfBirth",
      label: "Date of birth",
      input: true,
      format: "yyyy-MM-dd",
      enableDate: true,
      enableTime: false,
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Submit",
      input: true,
    },
  ],
} as unknown as FormType;

async function renderForm(
  options: Record<string, unknown> = { noAlerts: true },
) {
  const submissions: Record<string, unknown>[] = [];
  const screen = await render(
    <Form
      src={spec}
      options={options}
      onSubmit={(submission: { data: Record<string, unknown> }) => {
        submissions.push(submission.data);
      }}
    />,
  );
  return { screen, submissions };
}

afterEach(() => {
  setErrorCatalogue(undefined);
  vi.restoreAllMocks();
});

describe("design system fields", () => {
  it("draws each stock type with the design system and marks required fields", async () => {
    const { screen } = await renderForm();

    const firstName = screen.getByRole("textbox", { name: "First name" });
    await expect.element(firstName).toBeVisible();
    expect(
      firstName.element().closest(".bcds-react-aria-TextField"),
    ).not.toBeNull();
    // The marker the design system draws from `isRequired`.
    await expect.element(screen.getByText("(required)").first()).toBeVisible();
    // The name the error summary focuses by, on every field.
    await expect.element(firstName).toHaveAttribute("name", "data[firstName]");
    await expect
      .element(screen.getByRole("checkbox", { name: "I agree" }))
      .toHaveAttribute("name", "data[agrees]");
    // No Form.io-styled input is left.
    expect(document.querySelector("input.form-control")).toBeNull();
  });

  it("stays silent until a submit attempt, then shows each field's message", async () => {
    const { screen, submissions } = await renderForm();

    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toBeVisible();
    expect(document.body.textContent).not.toContain("is required");

    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(screen.getByText(/First name is required/).first())
      .toBeVisible();
    await expect
      .element(screen.getByText("Agreement is required").first())
      .toBeVisible();
    expect(submissions).toHaveLength(0);
  });

  it("words a rule failure from the field, then the catalogue, then the compiled text", async () => {
    setErrorCatalogue({
      "IDA.SIN.INVALID_CHECKSUM": "Catalogue wording for a bad SIN",
    });
    const { screen, submissions } = await renderForm();

    await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
    await screen
      .getByRole("textbox", { name: "SIN", exact: true })
      .fill("050082830");
    await screen.getByRole("textbox", { name: "Other SIN" }).fill("050082830");
    await screen.getByText("I agree").click();
    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(screen.getByText("Enter the SIN from your card").first())
      .toBeVisible();
    await expect
      .element(screen.getByText("Catalogue wording for a bad SIN").first())
      .toBeVisible();
    expect(submissions).toHaveLength(0);

    // A valid SIN clears both and the submit goes through.
    await screen
      .getByRole("textbox", { name: "SIN", exact: true })
      .fill("050082833");
    await screen.getByRole("textbox", { name: "Other SIN" }).fill("050082833");
    await screen.getByRole("button", { name: "Submit" }).click();
    await vi.waitFor(() => expect(submissions).toHaveLength(1));
  });

  it("applies the input mask to typed text and submits the masked value", async () => {
    const { screen, submissions } = await renderForm();

    await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
    await screen.getByText("I agree").click();
    const phone = screen.getByRole("textbox", { name: "Phone" });
    await phone.fill("2505550199");
    await expect.element(phone).toHaveValue("(250) 555-0199");

    await screen.getByRole("button", { name: "Submit" }).click();
    await vi.waitFor(() => expect(submissions).toHaveLength(1));
    expect(submissions[0].phone).toBe("(250) 555-0199");
  });

  it("submits a number, a chosen option and a checked box in their own types", async () => {
    const { screen, submissions } = await renderForm();

    await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
    await screen.getByRole("textbox", { name: "Monthly income" }).fill("2000");
    await screen.getByRole("button", { name: "Phone type" }).click();
    await screen.getByRole("option", { name: "Cell" }).click();
    await screen.getByText("I agree").click();
    await screen.getByRole("button", { name: "Submit" }).click();

    await vi.waitFor(() => expect(submissions).toHaveLength(1));
    expect(submissions[0]).toMatchObject({
      firstName: "Ada",
      monthlyIncome: 2000,
      phoneType: "cell",
      agrees: true,
    });
  });

  it("renders every field disabled in read-only mode", async () => {
    const { screen } = await renderForm({ readOnly: true, noAlerts: true });

    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toBeDisabled();
    await expect
      .element(screen.getByRole("checkbox", { name: "I agree" }))
      .toBeDisabled();
    await expect
      .element(screen.getByRole("button", { name: "Phone type" }))
      .toBeDisabled();
  });

  it("treats an empty confirmation of a filled partner as a mismatch", async () => {
    const { screen, submissions } = await renderForm();

    await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
    await screen.getByText("I agree").click();
    await screen
      .getByRole("textbox", { name: "Email", exact: true })
      .fill("ada@example.com");
    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(
        screen.getByText("The two email addresses do not match.").first(),
      )
      .toBeVisible();
    expect(submissions).toHaveLength(0);

    await screen
      .getByRole("textbox", { name: "Email verification" })
      .fill("Ada@Example.com");
    await screen.getByRole("button", { name: "Submit" }).click();
    await vi.waitFor(() => expect(submissions).toHaveLength(1));
  });

  it("clears a blur-validated field's message as soon as it is corrected", async () => {
    // The wrappers have no native input for Form.io to watch blur on, so a
    // field already showing a message is re-checked on change instead.
    const { screen } = await renderForm();

    await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
    await screen.getByText("I agree").click();
    await screen.getByRole("textbox", { name: "Postal" }).fill("V8V");
    await screen.getByRole("button", { name: "Submit" }).click();
    await expect
      .element(screen.getByText("Invalid postal code format").first())
      .toBeVisible();

    await screen.getByRole("textbox", { name: "Postal" }).fill("V8V 1X4");

    await vi.waitFor(() => {
      expect(document.body.textContent).not.toContain(
        "Invalid postal code format",
      );
    });
  });

  it("keeps the links in a checkbox label, sanitised", async () => {
    const { screen } = await renderForm();

    await expect
      .element(screen.getByRole("link", { name: "Terms of Use" }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("link", { name: "Privacy Statement" }))
      .toHaveAttribute("href", "https://example.gov.bc.ca/privacy");
    await expect
      .element(
        screen.getByRole("checkbox", {
          name: /I accept the Terms of Use and Privacy Statement/,
        }),
      )
      .toBeVisible();
  });

  it("re-checks a confirmation when the field it confirms changes", async () => {
    const { screen } = await renderForm();

    await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
    await screen.getByText("I agree").click();
    await screen
      .getByRole("textbox", { name: "Email", exact: true })
      .fill("ada@example.com");
    await screen
      .getByRole("textbox", { name: "Email verification" })
      .fill("ada@exampel.com");
    await screen.getByRole("button", { name: "Submit" }).click();
    await expect
      .element(
        screen.getByText("The two email addresses do not match.").first(),
      )
      .toBeVisible();

    // Fixing the email rather than the verification clears the verification's
    // message without waiting for the next submit.
    await screen
      .getByRole("textbox", { name: "Email", exact: true })
      .fill("ada@exampel.com");

    await vi.waitFor(() => {
      expect(document.body.textContent).not.toContain(
        "The two email addresses do not match.",
      );
    });
  });
});
