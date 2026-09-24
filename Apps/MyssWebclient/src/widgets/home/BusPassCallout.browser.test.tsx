import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import BusPassCallout from "./BusPassCallout";

describe("BusPassCallout", () => {
    it("renders the bus pass callout content", async () => {
        const screen = await render(<BusPassCallout />);

        await expect.element(screen.getByRole("region", { name: "BC Bus Pass" })).toBeInTheDocument();
        await expect.element(screen.getByRole("heading", { name: "Need a bus pass?" })).toBeInTheDocument();
        await expect
            .element(screen.getByText("Apply for a bus pass online. Learn more about the", { exact: false }))
            .toBeInTheDocument();
    });

    it("links to the bus pass program and application", async () => {
        const screen = await render(<BusPassCallout />);

        await expect.element(screen.getByRole("link", { name: "bus pass program" })).toHaveAttribute(
            "href",
            "https://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/bus-pass",
        );
        await expect.element(screen.getByRole("link", { name: "Apply for a bus pass" })).toHaveAttribute(
            "href",
            "/buspass",
        );
    });
});
