import { describe, it, expect } from "vitest";
import { render } from "vitest-browser-react";

import ChequeCalendar from "./ChequeCalendar";

describe("ChequeCalendar", () => {
    it("renders the next cheque issue date and days-from-now", async () => {
        const screen = await render(<ChequeCalendar />);

        await expect.element(screen.getByText("July 29", { exact: true })).toBeInTheDocument();
        await expect.element(screen.getByText("17 days from now")).toBeInTheDocument();
    });

    it("renders a link to the full payment schedule", async () => {
        const screen = await render(<ChequeCalendar />);

        const link = screen.getByRole("link", { name: /the full schedule for 2027/i });
        await expect.element(link).toHaveAttribute(
            "href",
            "https://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/payment-dates",
        );
    });
});
