import { Select, TextField } from "@bcgov/design-system-react-components";
import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import { SubmissionRejectedError } from "../api/forms";
import SubmissionErrors from "./SubmissionErrors";

describe("SubmissionErrors", () => {
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
});
