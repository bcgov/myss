import { expect, test } from "vitest";
import { render } from "vitest-browser-react";

import SiteFooter from "./SiteFooter";

test("renders the footer landmark and administrative links", async () => {
    const screen = await render(<SiteFooter />);

    await expect.element(screen.getByRole("contentinfo")).toBeVisible();

    for (const label of [
        "Home",
        "Disclaimer",
        "Privacy",
        "Terms of Use",
        "Accessibility",
        "Copyright",
    ]) {
        await expect.element(screen.getByRole("link", { name: label })).toBeVisible();
    }
});

test("uses the configured targets for external footer links", async () => {
    const screen = await render(<SiteFooter />);

    await expect
        .element(screen.getByRole("link", { name: "Privacy" }))
        .toHaveAttribute("target", "_blank");
    await expect
        .element(screen.getByRole("link", { name: "Terms of Use" }))
        .toHaveAttribute("target", "_blank");
    await expect
        .element(screen.getByRole("link", { name: "Home" }))
        .toHaveAttribute("target", "_self");
});
