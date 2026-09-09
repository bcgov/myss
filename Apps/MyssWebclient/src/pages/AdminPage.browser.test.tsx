import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter } from "react-router";

import AdminPage from "./AdminPage";

describe("AdminPage", () => {
    it("links to form management", async () => {
        const screen = await render(
            <MemoryRouter>
                <AdminPage />
            </MemoryRouter>,
        );

        await expect
            .element(
                screen.getByRole("heading", {
                    level: 1,
                    name: "Administration",
                }),
            )
            .toBeVisible();
        await expect.element(screen.getByText("Form management")).toBeVisible();
        await expect
            .element(screen.getByRole("link", { name: "Form management" }))
            .toHaveAttribute("href", "/admin/form-management");
    });
});
