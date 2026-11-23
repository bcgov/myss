import { Select, TextField } from "@bcgov/design-system-react-components";
import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import { SubmissionRejectedError } from "../api/forms";
import SubmissionErrors from "./SubmissionErrors";

function renderSummary(
  fields: React.ReactNode,
  errors: { field: string; message: string }[],
) {
  return render(
    <>
      <SubmissionErrors
        error={
          new SubmissionRejectedError(
            422,
            errors.map((e) => ({ ...e, keyword: "FORM.FIELD.REQUIRED" })),
          )
        }
      />
      {fields}
    </>,
  );
}

describe("SubmissionErrors", () => {
  it("takes focus when it appears", async () => {
    const screen = await renderSummary(null, [
      { field: "x", message: "Fix x." },
    ]);

    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
  });

  it("focuses a Form.io field by its data name", async () => {
    const screen = await renderSummary(
      <input aria-label="First name" name="data[firstName]" />,
      [{ field: "firstName", message: "Enter a first name." }],
    );

    await screen.getByRole("button", { name: "Enter a first name." }).click();

    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .toHaveFocus();
  });

  it("focuses the date input's month from an error link", async () => {
    const screen = await render(
      <>
        <SubmissionErrors
          error={
            new SubmissionRejectedError(422, [
              {
                field: "dateOfBirth",
                keyword: "FORM.DATE.INVALID",
                message: "Enter a valid date of birth.",
              },
            ])
          }
        />
        {/* The shape @myss/formio draws for a date question. */}
        <div className="formio-component-dateOfBirth">
          <Select
            label="Month"
            placeholder="Select a month"
            items={[{ id: "1", label: "January" }]}
          />
          <TextField label="Day" />
          <TextField label="Year" />
          <input type="hidden" name="data[dateOfBirth]" value="" />
        </div>
      </>,
    );

    await screen
      .getByRole("button", { name: "Enter a valid date of birth." })
      .click();

    await expect
      .element(screen.getByRole("button", { name: /Month/ }))
      .toHaveFocus();
  });

  it("focuses the month of a repeat section row's date from an error link", async () => {
    const screen = await render(
      <>
        <SubmissionErrors
          error={
            new SubmissionRejectedError(422, [
              {
                field: "children[1].childDateOfBirth",
                keyword: "FORM.FIELD.REQUIRED",
                message: "Enter the second child's date of birth.",
              },
            ])
          }
        />
        {[0, 1].map((row) => (
          <fieldset key={row} aria-label={`Child Info ${row + 1} of 2`}>
            <div className="formio-component formio-component-childDateOfBirth">
              <Select
                label="Month"
                placeholder="Select a month"
                items={[{ id: "1", label: "January" }]}
              />
              <TextField label="Day" />
              <TextField label="Year" />
              <input
                type="hidden"
                name={`data[children][${row}][childDateOfBirth]`}
                value=""
              />
            </div>
          </fieldset>
        ))}
      </>,
    );

    await screen
      .getByRole("button", { name: "Enter the second child's date of birth." })
      .click();

    await expect
      .element(
        screen
          .getByRole("group", { name: "Child Info 2 of 2" })
          .getByRole("button", { name: /Month/ }),
      )
      .toHaveFocus();
  });

  it("focuses a field outside Form.io by its id", async () => {
    const screen = await renderSummary(
      <input aria-label="Last name" id="representativeLastName" />,
      [{ field: "representativeLastName", message: "Enter your last name." }],
    );

    await screen.getByRole("button", { name: "Enter your last name." }).click();

    await expect
      .element(screen.getByRole("textbox", { name: "Last name" }))
      .toHaveFocus();
  });

  it("focuses the first control a group with the id holds", async () => {
    const screen = await renderSummary(
      <div id="accountContext" role="radiogroup" aria-label="Who">
        <input type="radio" name="who" aria-label="Self" />
        <input type="radio" name="who" aria-label="Someone else" />
      </div>,
      [
        {
          field: "accountContext",
          message: "Select the option that describes you.",
        },
      ],
    );

    await screen
      .getByRole("button", { name: "Select the option that describes you." })
      .click();

    await expect
      .element(screen.getByRole("radio", { name: "Self" }))
      .toHaveFocus();
  });

  it("leaves focus alone when nothing matches", async () => {
    const screen = await renderSummary(null, [
      { field: "missing", message: "Something is wrong." },
    ]);
    const button = screen.getByRole("button", { name: "Something is wrong." });

    await button.click();

    await expect.element(button).toHaveFocus();
  });
});
