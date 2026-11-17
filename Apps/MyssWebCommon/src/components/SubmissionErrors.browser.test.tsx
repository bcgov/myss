import { DatePicker } from "@bcgov/design-system-react-components";
import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import { SubmissionRejectedError } from "../api/forms";
import SubmissionErrors from "./SubmissionErrors";

describe("SubmissionErrors", () => {
  it("focuses the design system date input's year segment from an error link", async () => {
    const screen = await render(
      <>
        <SubmissionErrors
          error={new SubmissionRejectedError(422, [
            {
              field: "dateOfBirth",
              keyword: "FORM.DATE.INVALID",
              message: "Enter a valid date of birth.",
            },
          ])}
        />
        <div className="formio-component-dateOfBirth">
          <DatePicker
            label="Date of birth"
            name="data[dateOfBirth]"
            value={null}
            isInvalid
            errorMessage="Enter a valid date of birth."
          />
        </div>
      </>,
    );

    await screen
      .getByRole("button", { name: "Enter a valid date of birth." })
      .click();

    await expect
      .element(screen.getByRole("spinbutton", { name: /year/i }))
      .toHaveFocus();
  });
});
