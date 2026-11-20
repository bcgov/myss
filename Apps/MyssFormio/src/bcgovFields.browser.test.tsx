import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import "@formio/js/dist/formio.form.min.css";
import { setErrorCatalogue } from "./errorCatalogue";
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
      type: "textfield",
      key: "markedPhone",
      label: "Marked phone",
      input: true,
      properties: { myssValidator: "phone" },
    },
    {
      type: "phoneNumber",
      key: "stockPhone",
      label: "Stock phone",
      input: true,
      inputMask: "999-999-9999",
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

  describe("date input", () => {
    async function renderDate(
      component: Record<string, unknown> = {},
      value = "",
      options: Record<string, unknown> = {},
    ) {
      const submissions: Record<string, unknown>[] = [];
      const screen = await render(
        <Form
          src={
            {
              display: "form",
              components: [
                {
                  type: "datetime",
                  key: "date",
                  label: "Date of birth",
                  input: true,
                  enableTime: false,
                  format: "yyyy-MM-dd",
                  ...component,
                },
                {
                  type: "button",
                  key: "submit",
                  action: "submit",
                  label: "Submit",
                },
              ],
            } as unknown as FormType
          }
          submission={{ data: { date: value } }}
          options={{ noAlerts: true, ...options }}
          onSubmit={(submission: { data: Record<string, unknown> }) => {
            submissions.push(submission.data);
          }}
        />,
      );
      const month = screen.getByRole("button", { name: /Month/ });
      const day = screen.getByRole("textbox", { name: "Day" });
      const year = screen.getByRole("textbox", { name: "Year" });
      /** Choose the month (null leaves it) and type the day and year. */
      async function enter(
        monthName: string | null,
        dayText: string,
        yearText: string,
      ) {
        if (monthName) {
          await month.click();
          await screen.getByRole("option", { name: monthName }).click();
        }
        await day.fill(dayText);
        await year.fill(yearText);
      }
      return {
        screen,
        submissions,
        month,
        day,
        year,
        enter,
        submit: screen.getByRole("button", { name: "Submit", exact: true }),
      };
    }

    /** The texts of the elements an input's aria-describedby names. */
    function descriptions(element: Element | null | undefined): string[] {
      const ids = element?.getAttribute("aria-describedby")?.split(" ") ?? [];
      return ids.map((id) => document.getElementById(id)?.textContent ?? "");
    }

    function monthInvalid(month: { element(): Element }): boolean {
      return (
        month
          .element()
          .closest(".bcds-react-aria-Select")
          ?.hasAttribute("data-invalid") ?? false
      );
    }

    it("draws the month, day and year under the question and submits the ISO date", async () => {
      const { screen, month, day, year, submit, submissions } =
        await renderDate({
          description: "Use the date on your identification.",
          validate: { required: true },
        });
      const group = screen.getByRole("group", { name: /Date of birth/ });
      await expect.element(group).toBeVisible();
      expect(descriptions(group.element())).toEqual([
        "Use the date on your identification.",
      ]);
      await expect.element(group.getByText("(required)")).toBeVisible();
      expect(month.element().closest(".bcds-react-aria-Select")).not.toBeNull();
      expect(
        day.element().closest(".bcds-react-aria-TextField"),
      ).not.toBeNull();
      await expect.element(month).toHaveTextContent("Select a month");

      await month.click();
      await screen.getByRole("option", { name: "February" }).click();
      await userEvent.tab();
      await expect.element(day).toHaveFocus();
      await userEvent.keyboard("29");
      await userEvent.tab();
      await expect.element(year).toHaveFocus();
      await userEvent.keyboard("1948");
      await userEvent.tab();
      await expect.element(submit).toHaveFocus();
      await userEvent.keyboard("{Enter}");
      await vi.waitFor(() => expect(submissions).toHaveLength(1));
      expect(submissions[0].date).toBe("1948-02-29");
      expect(
        document.querySelector('input[type="hidden"][name="data[date]"]'),
      ).toHaveProperty("value", "1948-02-29");
    });

    it("allows an empty optional date", async () => {
      const { submit, submissions } = await renderDate();
      await submit.click();
      await vi.waitFor(() => expect(submissions).toHaveLength(1));
      expect(submissions[0].date).toBe("");
    });

    it("marks every part for a missing required date and clears it after correction", async () => {
      const { screen, month, day, year, enter, submit, submissions } =
        await renderDate({
          validate: {
            required: true,
            customMessage: "Enter your date of birth.",
          },
        });
      await submit.click();
      await expect
        .element(screen.getByText("Enter your date of birth.", { exact: true }))
        .toBeVisible();
      expect(monthInvalid(month)).toBe(true);
      for (const field of [day, year]) {
        await expect.element(field).toHaveAttribute("aria-invalid", "true");
        expect(descriptions(field.element())).toEqual([
          "Enter your date of birth.",
        ]);
      }
      await enter("February", "29", "2000");
      await submit.click();
      await vi.waitFor(() => expect(submissions).toHaveLength(1));
      expect(submissions[0].date).toBe("2000-02-29");
      expect(monthInvalid(month)).toBe(false);
      await expect.element(year).not.toHaveAttribute("aria-invalid", "true");
    });

    it("stacks the messages of the parts that are wrong, and marks only those", async () => {
      const { screen, month, day, year, enter, submit, submissions } =
        await renderDate();
      await enter(null, "32", "1990");
      await submit.click();
      for (const message of ["A birth month is required", "Invalid Day"]) {
        await expect
          .element(screen.getByText(message, { exact: true }))
          .toBeVisible();
      }
      expect(submissions).toHaveLength(0);
      expect(monthInvalid(month)).toBe(true);
      expect(descriptions(day.element())).toEqual(["Invalid Day"]);
      await expect.element(year).not.toHaveAttribute("aria-invalid", "true");

      // Each correction is re-checked as it is made.
      await enter("March", "32", "1990");
      await expect
        .element(screen.getByText("A birth month is required", { exact: true }))
        .not.toBeInTheDocument();
      expect(monthInvalid(month)).toBe(false);
      await expect.element(day).toHaveAttribute("aria-invalid", "true");

      await day.fill("31");
      await expect
        .element(screen.getByText("Invalid Day", { exact: true }))
        .not.toBeInTheDocument();
      await submit.click();
      await vi.waitFor(() => expect(submissions).toHaveLength(1));
      expect(submissions[0].date).toBe("1990-03-31");
    });

    it("shows one date message when every part is wrong", async () => {
      const { screen, month, day, year, enter, submit } = await renderDate();
      await enter(null, "32", "19");
      await submit.click();
      await expect
        .element(screen.getByText("Enter a valid date.", { exact: true }))
        .toBeVisible();
      expect(
        document.querySelectorAll(".formio-component-date p"),
      ).toHaveLength(1);
      expect(monthInvalid(month)).toBe(true);
      for (const field of [day, year]) {
        expect(descriptions(field.element())).toEqual(["Enter a valid date."]);
      }
    });

    it("refuses parts that make no real date, on every part", async () => {
      const { screen, month, day, year, enter, submit, submissions } =
        await renderDate();
      await enter("February", "30", "2001");
      await submit.click();
      await expect
        .element(screen.getByText("Enter a valid date.", { exact: true }))
        .toBeVisible();
      expect(submissions).toHaveLength(0);
      expect(monthInvalid(month)).toBe(true);
      await expect.element(day).toHaveAttribute("aria-invalid", "true");
      await expect.element(year).toHaveAttribute("aria-invalid", "true");
    });

    it("keeps inclusive minimum and maximum date rules", async () => {
      const { year, enter, submit, submissions } = await renderDate({
        datePicker: { minDate: "1950-01-01", maxDate: "2000-12-31" },
      });
      await enter("December", "31", "1949");
      await submit.click();
      await expect.element(year).toHaveAttribute("aria-invalid", "true");
      expect(submissions).toHaveLength(0);
      await enter("January", "1", "1950");
      await submit.click();
      await vi.waitFor(() => expect(submissions).toHaveLength(1));
      await enter("January", "1", "2001");
      await submit.click();
      await expect.element(year).toHaveAttribute("aria-invalid", "true");
      expect(submissions).toHaveLength(1);
      await enter("December", "31", "2000");
      await submit.click();
      await vi.waitFor(() => expect(submissions).toHaveLength(2));
      expect(submissions.map((data) => data.date)).toEqual([
        "1950-01-01",
        "2000-12-31",
      ]);
    });

    it("retains form-specific date rules", async () => {
      const { year, enter, submit, submissions, screen } = await renderDate({
        validate: {
          custom: "valid = input < '2000-01-01' || 'Use a date before 2000.';",
        },
      });
      await enter("January", "1", "2001");
      await submit.click();
      await expect
        .element(screen.getByText("Use a date before 2000.", { exact: true }))
        .toBeVisible();
      expect(submissions).toHaveLength(0);
      await year.fill("1999");
      await submit.click();
      await vi.waitFor(() => expect(submissions).toHaveLength(1));
    });

    it.each(["2025-02-31", "not a date", "1950-12-31x"])(
      "shows empty parts for a stored value that is not a date: %s",
      async (value) => {
        const { month, day, year } = await renderDate({}, value, {
          readOnly: true,
        });
        await expect.element(month).toHaveTextContent("Select a month");
        await expect.element(day).toHaveValue("");
        await expect.element(year).toHaveValue("");
      },
    );

    it.each(["1950-12-31", "1950-12-31T23:00:00-08:00"])(
      "displays an existing answer without a timezone shift: %s",
      async (value) => {
        const { month, day, year } = await renderDate({}, value, {
          readOnly: true,
        });
        await expect.element(month).toHaveTextContent("December");
        await expect.element(day).toHaveValue("31");
        await expect.element(year).toHaveValue("1950");
        await expect.element(month).toBeDisabled();
        await expect.element(day).toBeDisabled();
        await expect.element(year).toBeDisabled();
      },
    );
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

  it("formats a legacy masked phone and submits the displayed value", async () => {
    const { screen, submissions } = await renderForm();

    await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
    await screen.getByText("I agree").click();
    const phone = screen.getByRole("textbox", { name: "Phone", exact: true });
    await phone.fill("2505550199");
    await expect.element(phone).toHaveValue("(250) 555-0199");

    await screen.getByRole("button", { name: "Submit" }).click();
    await vi.waitFor(() => expect(submissions).toHaveLength(1));
    expect(submissions[0].phone).toBe("(250) 555-0199");
  });

  it("keeps progressive typing and deletion usable from the keyboard", async () => {
    const { screen } = await renderForm();
    const phone = screen.getByRole("textbox", { name: "Marked phone" });
    const input = phone.element() as HTMLInputElement;
    await phone.click();
    for (const [key, shown] of [
      ["2", "(2"],
      ["5", "(25"],
      ["0", "(250)"],
      ["5", "(250) 5"],
      ["5", "(250) 55"],
      ["5", "(250) 555"],
      ["0", "(250) 555-0"],
      ["1", "(250) 555-01"],
      ["9", "(250) 555-019"],
      ["9", "(250) 555-0199"],
    ]) {
      await userEvent.keyboard(key);
      await expect.element(phone).toHaveValue(shown);
    }

    // An eleventh digit is refused, at the end or in the middle.
    await userEvent.keyboard("7");
    await expect.element(phone).toHaveValue("(250) 555-0199");
    input.setSelectionRange(7, 7);
    await userEvent.keyboard("7");
    await expect.element(phone).toHaveValue("(250) 555-0199");
    await vi.waitFor(() => expect(input.selectionStart).toBe(7));

    input.setSelectionRange(14, 14);
    await userEvent.keyboard("{Backspace}");
    await expect.element(phone).toHaveValue("(250) 555-019");
    await userEvent.keyboard("9");
    await expect.element(phone).toHaveValue("(250) 555-0199");
    input.setSelectionRange(7, 8);
    await userEvent.keyboard("6");
    await expect.element(phone).toHaveValue("(250) 565-0199");
    await vi.waitFor(() => expect(input.selectionStart).toBe(8));
  });

  it.each([
    ["Delete before the hyphen", "Delete", 9, "(250) 555-199", 9],
    ["Delete before the space", "Delete", 5, "(250) 550-199", 4],
    ["Delete before the bracket", "Delete", 4, "(250) 550-199", 4],
    ["Backspace after the hyphen", "Backspace", 10, "(250) 550-199", 8],
    ["Backspace after the bracket", "Backspace", 5, "(255) 550-199", 3],
  ])(
    "%s removes the neighbouring digit instead of stalling",
    async (_name, key, caret, expected, expectedCaret) => {
      const { screen } = await renderForm();
      const phone = screen.getByRole("textbox", { name: "Marked phone" });
      const input = phone.element() as HTMLInputElement;
      await phone.fill("2505550199");
      await expect.element(phone).toHaveValue("(250) 555-0199");
      input.focus();
      input.setSelectionRange(caret as number, caret as number);

      await userEvent.keyboard(`{${key}}`);
      await expect.element(phone).toHaveValue(expected as string);
      await vi.waitFor(() => expect(input.selectionStart).toBe(expectedCaret));
    },
  );

  it.each([
    ["a trailing letter", "2505550199x", "Backspace", 11],
    ["a leading plus", "+2505550199", "Delete", 0],
  ])("removing %s keeps every digit", async (_name, typed, key, caret) => {
    const { screen } = await renderForm();
    const phone = screen.getByRole("textbox", { name: "Marked phone" });
    const input = phone.element() as HTMLInputElement;
    await phone.fill(typed);
    await expect.element(phone).toHaveValue(typed);
    input.focus();
    input.setSelectionRange(caret as number, caret as number);

    await userEvent.keyboard(`{${key}}`);
    await expect.element(phone).toHaveValue("(250) 555-0199");
  });

  it("keeps deleting forward through the hyphen and brackets", async () => {
    const { screen } = await renderForm();
    const phone = screen.getByRole("textbox", { name: "Marked phone" });
    const input = phone.element() as HTMLInputElement;
    await phone.fill("2505550199");
    input.focus();
    input.setSelectionRange(9, 9);

    await userEvent.keyboard("{Delete}");
    await expect.element(phone).toHaveValue("(250) 555-199");
    await userEvent.keyboard("{Delete}");
    await expect.element(phone).toHaveValue("(250) 555-99");
    await userEvent.keyboard("{Delete}");
    await userEvent.keyboard("{Delete}");
    await expect.element(phone).toHaveValue("(250) 555");
    // Nothing left to remove: the value stays.
    await userEvent.keyboard("{Delete}");
    await expect.element(phone).toHaveValue("(250) 555");
  });

  it.each(["Phone", "Marked phone", "Stock phone"])(
    "accepts pasted separators, keeps ten digits and reports a short number on %s",
    async (label) => {
      const { screen } = await renderForm();
      const phone = screen.getByRole("textbox", { name: label, exact: true });

      await phone.fill("  2 5 0 ( 5 5 5 ) - 0 1 9 9  ");
      await expect.element(phone).toHaveValue("(250) 555-0199");
      await expect.element(phone).toHaveAttribute("type", "tel");
      await phone.fill("25055501999");
      await expect.element(phone).toHaveValue("(250) 555-0199");
      await phone.fill("250555019");
      await expect.element(phone).toHaveValue("(250) 555-019");
      await screen.getByRole("textbox", { name: "First name" }).fill("Ada");
      await screen.getByText("I agree").click();
      await screen.getByRole("button", { name: "Submit" }).click();
      await expect.element(phone).toHaveAttribute("aria-invalid", "true");
      await expect
        .element(screen.getByText("Phone number is invalid").first())
        .toBeVisible();
      const input = phone.element();
      const errorIds = [
        input.getAttribute("aria-describedby"),
        input.getAttribute("aria-errormessage"),
      ].flatMap((ids) => ids?.split(/\s+/) ?? []);
      expect(
        errorIds.some((id) =>
          document
            .getElementById(id)
            ?.textContent?.includes("Phone number is invalid"),
        ),
      ).toBe(true);

      await phone.fill("2505550199");
      await expect.element(phone).toHaveValue("(250) 555-0199");
      await expect.element(phone).not.toHaveAttribute("aria-invalid", "true");
    },
  );

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
