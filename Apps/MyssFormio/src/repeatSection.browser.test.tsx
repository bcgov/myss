import { Form } from "@formio/react";
import type { FormType, JSON as FormioJson } from "@formio/react/lib/components/Form";
import { userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { describe, expect, it, vi } from "vitest";

import "@formio/js/dist/formio.form.min.css";
import { registerBcgovComponents } from "./bcgovComponents";

// The repeat section as a citizen meets it: one "Child Info" group per child
// counted, kept in step with the count, answered and submitted row by row.

registerBcgovComponents();

const COUNT_LABEL = "How many dependent children under the age of 19 live with you?";
const FIRST_NAME = "What is this child's first name on their government-issued ID?";
const LAST_NAME = "What is this child's last name on their government-issued ID?";
const DATE_OF_BIRTH = "What is this child's date of birth?";

const showWhenYes = { show: true, when: "hasChildren", eq: "yes" };

function childrenSpec(rowFields: unknown[]): FormType {
  return {
    display: "form",
    components: [
      {
        type: "radio",
        key: "hasChildren",
        label: "Do you have any dependent children under the age of 19 living with you?",
        input: true,
        values: [
          { label: "Yes", value: "yes" },
          { label: "No", value: "no" },
        ],
      },
      {
        type: "number",
        key: "childCount",
        label: COUNT_LABEL,
        input: true,
        validate: { min: 1, max: 10 },
        conditional: showWhenYes,
      },
      {
        type: "bcgovRepeatSection",
        key: "children",
        label: "Children",
        input: true,
        properties: { myssRepeatCount: "childCount", myssRepeatMax: "10", myssRowLabel: "Child Info" },
        conditional: showWhenYes,
        components: rowFields,
      },
      { type: "button", key: "submit", action: "submit", label: "Submit", input: true },
    ],
  } as unknown as FormType;
}

const spec = childrenSpec([
  { type: "textfield", key: "childFirstName", label: FIRST_NAME, input: true },
  { type: "textfield", key: "childLastName", label: LAST_NAME, input: true },
  {
    type: "datetime",
    key: "childDateOfBirth",
    label: DATE_OF_BIRTH,
    input: true,
    format: "yyyy-MM-dd",
    enableDate: true,
    enableTime: false,
  },
]);

/** The Form.io instance, as @formio/react hands it to onFormReady. */
interface Webform {
  data: Record<string, unknown>;
  on(event: string, listener: () => void): void;
}

async function renderForm(
  form: FormType = spec,
  props: { options?: Record<string, unknown>; submission?: { data: { [key: string]: FormioJson } } } = {},
) {
  const submissions: Record<string, unknown>[] = [];
  let instance: Webform | undefined;
  const screen = await render(
    <Form
      src={form}
      options={props.options ?? { noAlerts: true }}
      submission={props.submission}
      onFormReady={(ready: unknown) => {
        instance = ready as Webform;
      }}
      onSubmit={(submission: { data: Record<string, unknown> }) => {
        submissions.push(submission.data);
      }}
    />,
  );
  await vi.waitFor(() => expect(instance).toBeDefined());
  return { screen, submissions, form: instance as Webform };
}

/**
 * Resolves after the form has handled a change that satisfies `done`. Form.io
 * batches changes typed in quick succession, so a test that types a blank
 * and then a number at once never shows the form the blank.
 */
function changeHandled(form: Webform, done: (data: Record<string, unknown>) => boolean) {
  return new Promise<void>((resolve) => {
    form.on("change", () => {
      if (done(form.data)) resolve();
    });
  });
}

type Screen = Awaited<ReturnType<typeof renderForm>>["screen"];

/** The row groups, by the accessible name each one's heading gives it. */
function row(screen: Screen, index: number, total: number) {
  return screen.getByRole("group", { name: `Child Info ${index} of ${total}`, exact: true });
}

function rowGroups(): Element[] {
  return Array.from(document.querySelectorAll("fieldset")).filter((fieldset) =>
    fieldset.querySelector("legend")?.textContent?.startsWith("Child Info"),
  );
}

async function answerYes(screen: Screen) {
  await screen.getByText("Yes", { exact: true }).click();
}

/** Types the count and leaves the field, retrying past Form.io's validation redraw. */
async function enterCount(screen: Screen, value: string) {
  const field = screen.getByRole("textbox", { name: COUNT_LABEL });
  await expect
    .poll(
      async () => {
        await field.fill(value);
        await userEvent.tab();
        return (field.element() as HTMLInputElement).value;
      },
      { timeout: 10_000 },
    )
    .toBe(value);
}

async function expectRowCount(count: number) {
  await expect.poll(() => rowGroups().length, { timeout: 5_000 }).toBe(count);
}

describe("bcgovRepeatSection", () => {
  it("shows no rows until the count is answered", async () => {
    const { screen } = await renderForm();

    await answerYes(screen);

    await expect.element(screen.getByRole("textbox", { name: COUNT_LABEL })).toBeVisible();
    expect(rowGroups()).toHaveLength(0);
  });

  it("shows one Child Info group per child, each with the three questions", async () => {
    const { screen } = await renderForm();
    await answerYes(screen);

    await enterCount(screen, "3");

    await expectRowCount(3);
    for (const index of [1, 2, 3]) {
      const group = row(screen, index, 3);
      await expect.element(group.getByText("Child Info")).toBeVisible();
      await expect.element(group.getByRole("textbox", { name: FIRST_NAME })).toBeVisible();
      await expect.element(group.getByRole("textbox", { name: LAST_NAME })).toBeVisible();
      await expect.element(group.getByRole("group", { name: DATE_OF_BIRTH })).toBeVisible();
    }
  });

  it("removes the last group when the count goes down, and says so", async () => {
    const { screen } = await renderForm();
    await answerYes(screen);
    await enterCount(screen, "3");
    await expectRowCount(3);

    await enterCount(screen, "2");

    await expectRowCount(2);
    await expect.element(row(screen, 2, 2)).toBeVisible();
    await expect
      .poll(() => document.querySelector('[aria-live="polite"][ref="liveRegion"]')?.textContent)
      .toBe("Child Info 3 removed.");
  });

  it("keeps the rows already answered while the count is retyped", async () => {
    const { screen, form } = await renderForm();
    await answerYes(screen);
    await enterCount(screen, "2");
    await expectRowCount(2);
    await row(screen, 1, 2).getByRole("textbox", { name: FIRST_NAME }).fill("Ada");
    await row(screen, 2, 2).getByRole("textbox", { name: FIRST_NAME }).fill("Byron");

    const blankHandled = changeHandled(form, (data) => data.childCount === undefined);
    await screen.getByRole("textbox", { name: COUNT_LABEL }).fill("");
    await blankHandled;
    expect(form.data.children).toHaveLength(2);
    await userEvent.tab();
    await enterCount(screen, "4");

    await expectRowCount(4);
    await expect
      .element(row(screen, 1, 4).getByRole("textbox", { name: FIRST_NAME }))
      .toHaveValue("Ada");
    await expect
      .element(row(screen, 2, 4).getByRole("textbox", { name: FIRST_NAME }))
      .toHaveValue("Byron");
    await expect
      .element(row(screen, 3, 4).getByRole("textbox", { name: FIRST_NAME }))
      .toHaveValue("");
  });

  it("adds no rows for a count above the maximum", async () => {
    const { screen } = await renderForm();
    await answerYes(screen);
    await enterCount(screen, "3");
    await expectRowCount(3);

    await enterCount(screen, "11");

    await expectRowCount(3);
  });

  it("submits one answers object per row under the section's key", async () => {
    const { screen, submissions } = await renderForm();
    await answerYes(screen);
    await enterCount(screen, "2");
    await expectRowCount(2);
    await row(screen, 1, 2).getByRole("textbox", { name: FIRST_NAME }).fill("Ada");
    await row(screen, 1, 2).getByRole("textbox", { name: LAST_NAME }).fill("Byron");
    await row(screen, 2, 2).getByRole("textbox", { name: FIRST_NAME }).fill("Allegra");
    await row(screen, 2, 2).getByRole("textbox", { name: LAST_NAME }).fill("Byron");

    await screen.getByRole("button", { name: "Submit" }).click();

    await vi.waitFor(() => expect(submissions).toHaveLength(1));
    expect(submissions[0]).toMatchObject({
      hasChildren: "yes",
      childCount: 2,
      children: [
        { childFirstName: "Ada", childLastName: "Byron" },
        { childFirstName: "Allegra", childLastName: "Byron" },
      ],
    });
  });

  it("shows a row's error on that row's field only", async () => {
    const form = childrenSpec([
      {
        type: "textfield",
        key: "childFirstName",
        label: FIRST_NAME,
        input: true,
        validate: { required: true },
      },
    ]);
    const { screen, submissions } = await renderForm(form);
    await answerYes(screen);
    await enterCount(screen, "2");
    await expectRowCount(2);
    await row(screen, 1, 2).getByRole("textbox", { name: FIRST_NAME }).fill("Ada");

    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(row(screen, 2, 2).getByRole("textbox", { name: FIRST_NAME }))
      .toHaveAttribute("aria-invalid", "true");
    await expect
      .element(row(screen, 1, 2).getByRole("textbox", { name: FIRST_NAME }))
      .not.toHaveAttribute("aria-invalid", "true");
    expect(submissions).toHaveLength(0);
  });

  it("hides the groups and clears their answers when the answer is No", async () => {
    const { screen, submissions } = await renderForm();
    await answerYes(screen);
    await enterCount(screen, "2");
    await expectRowCount(2);
    await row(screen, 1, 2).getByRole("textbox", { name: FIRST_NAME }).fill("Ada");

    await screen.getByText("No", { exact: true }).click();
    await expectRowCount(0);
    await screen.getByRole("button", { name: "Submit" }).click();

    await vi.waitFor(() => expect(submissions).toHaveLength(1));
    expect(submissions[0].children ?? []).toEqual([]);
  });

  it("names each row's inputs after their row, so one row's choice leaves another's alone", async () => {
    const form = childrenSpec([
      {
        type: "radio",
        key: "sharesLastName",
        label: "Does this child share your last name?",
        input: true,
        values: [
          { label: "Same", value: "yes" },
          { label: "Different", value: "no" },
        ],
      },
      { type: "textfield", key: "childFirstName", label: FIRST_NAME, input: true },
    ]);
    const { screen } = await renderForm(form);
    await answerYes(screen);
    await enterCount(screen, "2");
    await expectRowCount(2);

    await row(screen, 1, 2).getByText("Same", { exact: true }).click();
    await row(screen, 2, 2).getByText("Different", { exact: true }).click();

    await expect.element(row(screen, 1, 2).getByRole("radio", { name: "Same" })).toBeChecked();
    await expect
      .element(row(screen, 2, 2).getByRole("radio", { name: "Different" }))
      .toBeChecked();
    await expect
      .element(row(screen, 2, 2).getByRole("textbox", { name: FIRST_NAME }))
      .toHaveAttribute("name", "data[children][1][childFirstName]");
  });

  it("shows and hides a row's question by that row's own answer", async () => {
    const sharesLastName = "Does this child share your last name?";
    const form = childrenSpec([
      {
        type: "radio",
        key: "sharesLastName",
        label: sharesLastName,
        input: true,
        values: [
          { label: "Same", value: "yes" },
          { label: "Different", value: "no" },
        ],
      },
      {
        type: "textfield",
        key: "childLastName",
        label: LAST_NAME,
        input: true,
        conditional: { show: true, when: "sharesLastName", eq: "no" },
      },
    ]);
    const { screen } = await renderForm(form);
    await answerYes(screen);
    await enterCount(screen, "2");
    await expectRowCount(2);

    await row(screen, 2, 2).getByText("Different", { exact: true }).click();

    await expect.element(row(screen, 2, 2).getByRole("textbox", { name: LAST_NAME })).toBeVisible();
    await expect
      .element(row(screen, 1, 2).getByRole("textbox", { name: LAST_NAME }))
      .not.toBeInTheDocument();
  });

  it("draws saved rows, and leaves them as saved in read-only mode", async () => {
    const children = [
      { childFirstName: "Ada", childLastName: "Byron" },
      { childFirstName: "Allegra", childLastName: "Byron" },
      { childFirstName: "Annabella", childLastName: "Byron" },
    ];
    const { screen } = await renderForm(spec, {
      options: { readOnly: true, noAlerts: true },
      submission: { data: { hasChildren: "yes", childCount: 2, children } },
    });

    await expectRowCount(3);
    await expect
      .element(row(screen, 3, 3).getByRole("textbox", { name: FIRST_NAME }))
      .toHaveValue("Annabella");
  });
});
