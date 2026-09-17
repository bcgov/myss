import { expect, test } from "vitest";
import { render } from "vitest-browser-react";

import SiteHeader from "./SiteHeader";

test("renders the government banner and ministry identity", async () => {
    const screen = await render(<SiteHeader />);

    await expect.element(screen.getByRole("banner")).toBeVisible();
    await expect
        .element(screen.getByRole("img", { name: "Government of BC" }))
        .toBeVisible();
    await expect.element(screen.getByText("Ministry of")).toBeVisible();
    await expect
        .element(screen.getByText("Social Development"))
        .toBeVisible();
    await expect
        .element(screen.getByText("and Poverty Reduction"))
        .toBeVisible();
});

test("provides accessible skip and accessibility links", async () => {
    const screen = await render(<SiteHeader />);

    const skipLink = screen.getByRole("link", {
        name: "Skip to main content",
    });
    await expect.element(skipLink).toHaveAttribute("href", "#main-content-anchor");

    const accessibilityLink = screen.getByRole("link", {
        name: "Accessibility Statement",
    });
    await expect
        .element(accessibilityLink)
        .toHaveAttribute(
            "href",
            "http://www2.gov.bc.ca/gov/content/home/accessibility",
        );
});
