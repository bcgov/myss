import { Formio } from "@formio/js";
import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import "@formio/js/dist/formio.form.min.css";
import { registerBcgovComponents } from "./bcgovComponents";

// The accordion body is HTML that reaches the browser from the form spec, so a
// content designer with edit access could put a payload in it. It renders
// through dangerouslySetInnerHTML, guarded only by the Utils.sanitize call in
// bcgovComponents. These tests are that guard's only coverage.
//
// The payloads set a flag rather than calling alert(): an inline handler that
// survived sanitization would fire and set it, which is a behavioural
// assertion rather than an inspection of the markup. A `<script>` tag is
// deliberately NOT used — browsers never execute scripts inserted via
// innerHTML, so it would pass with the sanitizer removed.

declare global {
  interface Window {
    __xssFired?: boolean;
  }
}

function accordionSpec(body: string): FormType {
  return {
    display: "form",
    components: [
      {
        type: "bcgovAccordion",
        key: "help",
        accordionLabel: "Open me",
        accordionBody: body,
        input: false,
      },
    ],
  } as unknown as FormType;
}

registerBcgovComponents();

afterEach(() => {
  delete window.__xssFired;
});

describe("bcgovAccordion body sanitization", () => {
  it("renders safe markup in the body", async () => {
    const screen = await render(
      <Form src={accordionSpec("<p>Plain guidance text.</p>")} />,
    );

    await screen.getByRole("button", { name: "Open me" }).click();
    await expect
      .element(screen.getByText("Plain guidance text."))
      .toBeVisible();
  });

  it("strips an inline event handler that would otherwise run", async () => {
    const screen = await render(
      <Form
        src={accordionSpec(
          '<p>Still here.</p><img src="x" onerror="window.__xssFired = true">',
        )}
      />,
    );

    await screen.getByRole("button", { name: "Open me" }).click();
    // The surrounding markup renders, so the body really did reach the DOM —
    // without this the absence of the handler would prove nothing.
    await expect.element(screen.getByText("Still here.")).toBeVisible();

    const image = document.querySelector("img[src='x']");
    expect(
      image,
      "the img was removed entirely, so nothing was proven",
    ).not.toBeNull();
    expect(image?.getAttribute("onerror")).toBeNull();
    expect(window.__xssFired).toBeUndefined();
  });

  it("strips a javascript: link target", async () => {
    const screen = await render(
      <Form
        src={accordionSpec(
          '<a href="javascript:window.__xssFired = true">Read more</a>',
        )}
      />,
    );

    await screen.getByRole("button", { name: "Open me" }).click();

    const link = document.querySelector("a");
    expect(link?.getAttribute("href") ?? "").not.toContain("javascript:");
    expect(window.__xssFired).toBeUndefined();
  });
});

function radioSpec(required: boolean): FormType {
  return {
    display: "form",
    components: [
      {
        type: "bcgovRadio",
        key: "gender",
        label: "Gender",
        input: true,
        values: [
          { label: "Man/Boy", value: "man" },
          { label: "Woman/Girl", value: "woman" },
        ],
        validate: { required },
      },
    ],
  } as unknown as FormType;
}

describe("bcgovRadio required state", () => {
  // Flat groups used to leave isRequired off so BCDS would not print
  // "(required)"; assistive technology then heard the field as optional.
  it("reports a required flat group as required, by name and by state", async () => {
    const screen = await render(<Form src={radioSpec(true)} />);

    const group = screen.getByRole("radiogroup", { name: "Gender (required)" });
    await expect.element(group).toBeInTheDocument();
    await expect.element(group).toHaveAttribute("aria-required", "true");
    await expect.element(screen.getByText("(required)")).toBeVisible();
  });

  it("leaves an optional group unmarked", async () => {
    const screen = await render(<Form src={radioSpec(false)} />);

    const group = screen.getByRole("radiogroup", { name: "Gender" });
    await expect.element(group).toBeInTheDocument();
    await expect.element(group).not.toHaveAttribute("aria-required", "true");
    expect(document.querySelector("[data-myss-required]")).toBeNull();
  });
});

function calloutSpec(callout: Record<string, unknown>): FormType {
  return {
    display: "form",
    components: [
      {
        type: "bcgovCallout",
        key: "privacyCallout",
        input: false,
        components: [
          { type: "content", key: "notice", html: "<p>Privacy notice.</p>" },
          {
            type: "textfield",
            key: "firstName",
            label: "First name",
            input: true,
          },
        ],
        ...callout,
      },
    ],
  } as unknown as FormType;
}

describe("bcgovCallout", () => {
  it("renders nested components inside the design system callout", async () => {
    const screen = await render(
      <Form src={calloutSpec({ variant: "lightBlue" })} />,
    );

    await expect.element(screen.getByText("Privacy notice.")).toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toBeVisible();
    const box = document.querySelector(".bcds-Callout.lightBlue");
    expect(box?.textContent).toContain("Privacy notice.");
    expect(getComputedStyle(box as Element).borderLeftWidth).toBe("8px");
  });

  it("shows the title as a heading at the level given", async () => {
    const screen = await render(
      <Form
        src={calloutSpec({ calloutTitle: "Your privacy", headingLevel: 3 })}
      />,
    );

    await expect
      .element(screen.getByRole("heading", { level: 3, name: "Your privacy" }))
      .toBeVisible();
  });

  it("writes the title as text, never as markup", async () => {
    await render(
      <Form
        src={calloutSpec({ calloutTitle: '<img src="x" onerror="1">Title' })}
      />,
    );

    expect(document.querySelector(".bcds-Callout img")).toBeNull();
    expect(document.querySelector(".bcds-Callout h2")?.textContent).toBe(
      '<img src="x" onerror="1">Title',
    );
  });
});

function inlineAlertSpec(alert: Record<string, unknown>): FormType {
  return {
    display: "form",
    components: [
      {
        type: "bcgovInlineAlert",
        key: "eligibilityAlert",
        input: false,
        components: [
          { type: "content", key: "criteria", html: "<p>Criteria text.</p>" },
          {
            type: "checkbox",
            key: "acknowledged",
            label: "I have read the criteria.",
            input: true,
          },
        ],
        ...alert,
      },
    ],
  } as unknown as FormType;
}

describe("bcgovInlineAlert", () => {
  it("renders nested components inside the design system inline alert, with its icon", async () => {
    const screen = await render(
      <Form src={inlineAlertSpec({ variant: "info" })} />,
    );

    await expect.element(screen.getByText("Criteria text.")).toBeVisible();
    await expect
      .element(
        screen.getByRole("checkbox", { name: "I have read the criteria." }),
      )
      .toBeInTheDocument();
    const box = document.querySelector(".bcds-Inline-Alert.info");
    expect(box?.textContent).toContain("Criteria text.");
    expect(box?.querySelector(".bcds-Inline-Alert--icon svg")).not.toBeNull();
    expect(getComputedStyle(box as Element).borderTopWidth).toBe("1px");
    expect(
      getComputedStyle(screen.getByText("Criteria text.").element()).fontSize,
    ).toBe("16px");
  });

  it("shows the title in bold, as text, never as markup", async () => {
    await render(
      <Form
        src={inlineAlertSpec({ alertTitle: '<img src="x">Eligibility' })}
      />,
    );

    const title = document.querySelector(
      ".bcds-Inline-Alert--container > .title",
    );
    expect(title?.textContent).toBe('<img src="x">Eligibility');
    expect(document.querySelector(".bcds-Inline-Alert img")).toBeNull();
    expect(getComputedStyle(title as Element).fontWeight).toBe("700");
  });

  it("falls back to the information kind for an unknown variant", async () => {
    await render(<Form src={inlineAlertSpec({ variant: "purple" })} />);

    await expect
      .poll(() => document.querySelector(".bcds-Inline-Alert.info"))
      .not.toBeNull();
  });
});

function checkboxSpec(checkbox: Record<string, unknown>): FormType {
  return {
    display: "form",
    components: [
      {
        type: "checkbox",
        key: "eligibilityAcknowledged",
        label: "I have read the BC Bus Pass Program eligibility criteria.",
        input: true,
        validate: { required: true },
        ...checkbox,
      },
    ],
  } as unknown as FormType;
}

describe("checkbox under a question", () => {
  it("draws the question above the checkbox, with (required) on the question only", async () => {
    const screen = await render(
      <Form
        src={checkboxSpec({
          properties: { myssGroupLabel: "Acknowledgement" },
        })}
      />,
    );

    await expect
      .element(
        screen.getByRole("group", { name: "Acknowledgement (required)" }),
      )
      .toBeInTheDocument();
    const checkbox = screen.getByRole("checkbox", {
      name: "I have read the BC Bus Pass Program eligibility criteria.",
      exact: true,
    });
    await expect.element(checkbox).toBeInTheDocument();
    expect(document.querySelectorAll("[data-myss-required]")).toHaveLength(1);
  });

  it("keeps (required) on the checkbox's own text without a question", async () => {
    const screen = await render(<Form src={checkboxSpec({})} />);

    await expect
      .element(
        screen.getByRole("checkbox", {
          name: "I have read the BC Bus Pass Program eligibility criteria. (required)",
        }),
      )
      .toBeInTheDocument();
    expect(document.querySelector("[role='group']")).toBeNull();
  });
});

function numberSpec(number: Record<string, unknown>): FormType {
  return {
    display: "form",
    components: [
      {
        type: "number",
        key: "monthlyIncome",
        label: "Your Monthly Income",
        input: true,
        ...number,
      },
    ],
  } as unknown as FormType;
}

/** The answers of the one form on the page, as Form.io holds them. */
function formData(): Record<string, unknown> {
  const forms = Object.values(
    (
      Formio as unknown as {
        forms: Record<string, { data: Record<string, unknown> }>;
      }
    ).forms,
  );
  return forms[forms.length - 1].data;
}

describe("number field", () => {
  it("is a plain text box, with the spec's prefix and no steppers", async () => {
    const screen = await render(<Form src={numberSpec({ prefix: "$" })} />);

    await expect
      .element(screen.getByRole("textbox", { name: "Your Monthly Income" }))
      .toBeInTheDocument();
    expect(document.querySelector(".bcds-react-aria-NumberField")).toBeNull();
    expect(
      document.querySelector(".bcds-react-aria-TextField--container")
        ?.textContent,
    ).toContain("$");
  });

  it("stores what is typed as a number", async () => {
    const screen = await render(<Form src={numberSpec({})} />);
    const input = screen.getByRole("textbox", { name: "Your Monthly Income" });

    await input.fill("1250.5");

    await vi.waitFor(() => expect(formData().monthlyIncome).toBe(1250.5));
  });

  it("refuses text that cannot be a number", async () => {
    const screen = await render(<Form src={numberSpec({})} />);
    const input = screen.getByRole("textbox", { name: "Your Monthly Income" });

    await input.fill("12a");
    await expect.element(input).toHaveValue("");

    await input.fill("12.555");
    await expect.element(input).toHaveValue("");
  });

  it("shows the stored value tidied once the citizen leaves the field", async () => {
    const screen = await render(<Form src={numberSpec({ delimiter: true })} />);
    const input = screen.getByRole("textbox", { name: "Your Monthly Income" });

    await input.fill("1250000.50");
    await expect.element(input).toHaveValue("1250000.50");
    (input.element() as HTMLInputElement).blur();

    await expect.element(input).toHaveValue("1,250,000.5");
  });
});

describe("currency field (Amount)", () => {
  const currencySpec = (extra: Record<string, unknown> = {}) =>
    numberSpec({ type: "currency", ...extra });

  it("shows a $ for Canadian dollars by default, without a prefix in the spec", async () => {
    const screen = await render(<Form src={currencySpec()} />);

    await expect
      .element(screen.getByRole("textbox", { name: "Your Monthly Income" }))
      .toBeInTheDocument();
    expect(
      document.querySelector(".bcds-react-aria-TextField--container")
        ?.textContent,
    ).toContain("$");
  });

  it("takes its sign from the currency code", async () => {
    await render(<Form src={currencySpec({ currency: "EUR" })} />);

    await vi.waitFor(() =>
      expect(
        document.querySelector(".bcds-react-aria-TextField--container")
          ?.textContent,
      ).toContain("€"),
    );
  });

  it("stores the amount as a number and shows it with grouping commas", async () => {
    const screen = await render(<Form src={currencySpec()} />);
    const input = screen.getByRole("textbox", { name: "Your Monthly Income" });

    await input.fill("1250.5");
    await vi.waitFor(() => expect(formData().monthlyIncome).toBe(1250.5));
    (input.element() as HTMLInputElement).blur();

    await expect.element(input).toHaveValue("1,250.5");
  });
});

describe("a field made required by another answer (Form.io logic)", () => {
  const requiredWhenEmail = [
    {
      name: "Required when email is preferred",
      trigger: {
        type: "simple",
        simple: { show: true, when: "preferredCommunication", eq: "email" },
      },
      actions: [
        {
          name: "Require",
          type: "property",
          property: {
            label: "Required",
            value: "validate.required",
            type: "boolean",
          },
          state: true,
        },
      ],
    },
  ];
  const spec = {
    display: "form",
    components: [
      {
        type: "radio",
        key: "preferredCommunication",
        label: "Preferred method of communication",
        input: true,
        values: [
          { label: "Phone", value: "phone" },
          { label: "Email", value: "email" },
        ],
      },
      {
        type: "email",
        key: "email",
        label: "Email",
        input: true,
        validate: { required: false },
        errors: { required: "An email address is required" },
        logic: requiredWhenEmail,
      },
      {
        type: "button",
        key: "submit",
        label: "Submit",
        action: "submit",
        input: true,
      },
    ],
  } as unknown as FormType;

  it("marks and enforces the field only while the other answer asks for it", async () => {
    const screen = await render(<Form src={spec} />);
    const emailLabel = () =>
      document.querySelector(".formio-component-email label")?.textContent ??
      "";

    await expect
      .element(screen.getByRole("textbox", { name: /Email/ }))
      .toBeInTheDocument();
    expect(emailLabel()).not.toContain("(required)");

    // The design system hides the real radio input, so click it directly.
    (
      screen.getByRole("radio", { name: "Email" }).element() as HTMLElement
    ).click();
    await vi.waitFor(() => expect(emailLabel()).toContain("(required)"));

    await screen.getByRole("button", { name: "Submit" }).click();
    // The field's own message (Form.io's alert list repeats it; the real
    // forms switch that list off).
    await vi.waitFor(() =>
      expect(
        document.querySelector(
          ".formio-component-email .bcds-react-aria-TextField--Error",
        )?.textContent,
      ).toBe("An email address is required"),
    );

    (
      screen.getByRole("radio", { name: "Phone" }).element() as HTMLElement
    ).click();
    await vi.waitFor(() => expect(emailLabel()).not.toContain("(required)"));
  });
});
