import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter } from "react-router";

import type { Roles } from "@/auth/useRoles";

const roles = vi.hoisted(
    () =>
        ({
            roles: [],
            isLoading: false,
            canReview: false,
        }) as Roles,
);

vi.mock("@/auth/useRoles", () => ({
    useRoles: () => roles,
}));

import AdminPage from "./AdminPage";

function renderPage() {
    return render(
        <MemoryRouter>
            <AdminPage />
        </MemoryRouter>,
    );
}

describe("AdminPage", () => {
    beforeEach(() => {
        roles.roles = [];
        roles.canReview = false;
    });

    it("links to form management", async () => {
        const screen = await renderPage();

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
            .toHaveAttribute("href", "/form-management");
    });

    it("offers the review queue to a caller who may review", async () => {
        roles.roles = ["WORKER"];
        roles.canReview = true;

        const screen = await renderPage();

        await expect
            .element(screen.getByRole("link", { name: "Applications to review" }))
            .toHaveAttribute("href", "/applications");
    });

    it("does not offer the review queue to an IDIR user without the worker role", async () => {
        const screen = await renderPage();

        await expect.element(screen.getByText("Form management")).toBeVisible();
        await expect
            .element(screen.getByText("Applications to review"))
            .not.toBeInTheDocument();
    });
});
