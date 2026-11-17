import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { userEvent } from "@vitest/browser/context";
import { render, type RenderResult } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import {
  ACCOUNT_CONTEXTS,
  type AccountCreationAnswers,
  HR3189_URL,
  openingAnswers,
} from "@/lib/accountContext";
import AccountCreationQuestions from "@/widgets/registration/AccountCreationQuestions";

const LEGAL =
  "I’m a legal representative who is creating an account on behalf of someone else";
const PARENT = "I’m a parent making an account for a minor child";
const HELPER = "I’m making an account for myself with help from someone else";
const SELF = "I’m making an account for myself (and family, if applicable)";
const INFO =
  "All remaining questions relate to the person you are representing.";

function Harness({
  initial = openingAnswers(),
  fixed = false,
  errors = {},
}: {
  initial?: AccountCreationAnswers;
  fixed?: boolean;
  errors?: Record<string, string>;
}) {
  const [answers, setAnswers] = useState(initial);
  return (
    <AccountCreationQuestions
      answers={answers}
      contexts={fixed ? null : ACCOUNT_CONTEXTS}
      documentMode="keep"
      canKeep
      errors={errors}
      onChange={(change) => setAnswers((a) => ({ ...a, ...change }))}
      onKeepFile={async () => {}}
      onUploaded={() => {}}
      onRemoveFile={() => {}}
    />
  );
}

// The design system hides the real radio input; a person, and React Aria,
// use the label.
function choose(screen: RenderResult, label: string) {
  return screen.getByText(label, { exact: true }).click();
}

function renderQuestions(props: Parameters<typeof Harness>[0] = {}) {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <Harness {...props} />
    </QueryClientProvider>,
  );
}

describe("AccountCreationQuestions", () => {
  it("names the options by the heading, with Self chosen", async () => {
    const screen = await renderQuestions();

    const group = screen.getByRole("radiogroup", {
      name: "Creating a My Self Serve account",
    });
    await expect.element(group).toBeVisible();
    await expect
      .element(screen.getByRole("radio", { name: SELF }))
      .toBeChecked();
    expect(
      screen.container.querySelectorAll('input[type="radio"]'),
    ).toHaveLength(4);
  });

  it("asks a legal representative for their details, the agency and power of attorney, with the info bar", async () => {
    const screen = await renderQuestions();

    await choose(screen, LEGAL);

    await expect
      .element(
        screen.getByRole("heading", {
          level: 3,
          name: "Enter your information as the authorized representative",
        }),
      )
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: /^First name/ }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: /^Last name/ }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: /^Phone number/ }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: "Agency (optional)" }))
      .toBeVisible();
    await expect
      .element(screen.getByText("Attach Power of Attorney documents below."))
      .toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Attach document" }))
      .toBeVisible();
    await expect.element(screen.getByText(INFO)).toBeVisible();
  });

  it("asks a parent for their details and shows the info bar, without the agency or a document", async () => {
    const screen = await renderQuestions();

    await choose(screen, PARENT);

    await expect
      .element(screen.getByRole("textbox", { name: /^First name/ }))
      .toBeVisible();
    await expect.element(screen.getByText(INFO)).toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: "Agency (optional)" }))
      .not.toBeInTheDocument();
    await expect
      .element(screen.getByRole("button", { name: "Attach document" }))
      .not.toBeInTheDocument();
  });

  it("removes a context's questions when another is chosen", async () => {
    const screen = await renderQuestions();
    await choose(screen, LEGAL);
    await expect.element(screen.getByText(INFO)).toBeVisible();

    await choose(screen, SELF);

    await expect.element(screen.getByText(INFO)).not.toBeInTheDocument();
    await expect
      .element(screen.getByRole("textbox", { name: /^First name/ }))
      .not.toBeInTheDocument();
  });

  it("asks With Helper for the authorization, starting at No, and for the consent form after Yes", async () => {
    const screen = await renderQuestions();

    await choose(screen, HELPER);

    const authorization = screen.getByRole("radiogroup", {
      name: "Do you authorize us to share your information with this person?",
    });
    await expect.element(authorization).toBeVisible();
    await expect
      .element(screen.getByRole("radio", { name: /^No, I would prefer/ }))
      .toBeChecked();
    await expect
      .element(screen.getByRole("button", { name: "Attach document" }))
      .not.toBeInTheDocument();

    await choose(screen, "Yes");

    const link = screen.getByRole("link", {
      name: "Consent to Disclosure of Information (PDF, opens in a new tab)",
    });
    await expect.element(link).toHaveAttribute("href", HR3189_URL);
    await expect.element(link).toHaveAttribute("target", "_blank");
    await expect
      .element(screen.getByRole("button", { name: "Attach document" }))
      .toBeVisible();
    await expect.element(screen.getByText(INFO)).not.toBeInTheDocument();
  });

  it("formats the representative's phone as it is typed", async () => {
    const screen = await renderQuestions({
      initial: { ...openingAnswers(), context: "parentForMinor" },
    });

    const phone = screen.getByRole("textbox", { name: /^Phone number/ });
    await userEvent.type(phone, "2505550123");

    await expect.element(phone).toHaveValue("(250) 555-0123");
  });

  it("shows a fixed context as text", async () => {
    const screen = await renderQuestions({
      initial: { ...openingAnswers(), context: "legalRepresentative" },
      fixed: true,
    });

    await expect
      .element(screen.getByText("Your answer:", { exact: false }))
      .toBeVisible();
    await expect.element(screen.getByText(LEGAL)).toBeVisible();
    await expect
      .element(
        screen.getByText(
          "To change this answer, select Cancel and start again.",
        ),
      )
      .toBeVisible();
    expect(screen.container.querySelector('[role="radiogroup"]')).toBeNull();
    await expect
      .element(screen.getByRole("textbox", { name: /^First name/ }))
      .toBeVisible();
  });

  it("shows each error on its field, and gives each field its request name as its id", async () => {
    const screen = await renderQuestions({
      initial: { ...openingAnswers(), context: "legalRepresentative" },
      errors: {
        representativeFirstName: "Enter your first name.",
        representativePhone: "Phone number is invalid",
      },
    });

    const first = screen.getByRole("textbox", { name: /^First name/ });
    await expect.element(first).toHaveAttribute("aria-invalid", "true");
    await expect
      .element(first)
      .toHaveAccessibleDescription("Enter your first name.");
    await expect
      .element(first)
      .toHaveAttribute("id", "representativeFirstName");
    await expect
      .element(screen.getByText("Phone number is invalid"))
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: /^Last name/ }))
      .not.toHaveAttribute("aria-invalid", "true");
  });

  it("is operable by keyboard", async () => {
    const screen = await renderQuestions();

    await choose(screen, SELF);
    await userEvent.keyboard("{ArrowDown}");
    await expect
      .element(screen.getByRole("radio", { name: HELPER }))
      .toBeChecked();

    await userEvent.keyboard("{ArrowDown}");
    await expect
      .element(screen.getByRole("radio", { name: LEGAL }))
      .toBeChecked();
    await userEvent.tab();
    await expect
      .element(screen.getByRole("textbox", { name: /^First name/ }))
      .toHaveFocus();
  });
});
