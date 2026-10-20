import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import type { Roles } from "@/auth/useRoles";
import { paths } from "@/routes/paths";

// RequireWorker sits inside RequireIdir, which owns sign-in; this guard only
// decides on the roles MyssAdminApi reports for /auth/me.
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

import RequireWorker from "./RequireWorker";

function renderGuard() {
    return render(
        <MemoryRouter initialEntries={[paths.applications]}>
            <Routes>
                <Route path={paths.home} element={<p>Admin landing</p>} />
                <Route
                    path={paths.applications}
                    element={
                        <RequireWorker>
                            <p>Review queue</p>
                        </RequireWorker>
                    }
                />
            </Routes>
        </MemoryRouter>,
    );
}

describe("RequireWorker", () => {
    beforeEach(() => {
        roles.roles = [];
        roles.isLoading = false;
        roles.canReview = false;
    });

    it("keeps waiting while the roles are still being fetched", async () => {
        // Deciding before /auth/me answers would turn a real worker away.
        roles.isLoading = true;

        const screen = await renderGuard();

        await expect.element(screen.getByText("Loading…")).toBeInTheDocument();
        await expect
            .element(screen.getByText("Review queue"))
            .not.toBeInTheDocument();
    });

    it("explains, without redirecting, to an IDIR user without the worker role", async () => {
        roles.roles = [];

        const screen = await renderGuard();

        await expect
            .element(screen.getByRole("alert"))
            .toHaveTextContent(/Ministry worker role/);
        await expect
            .element(screen.getByRole("link", { name: "Back to Administration" }))
            .toHaveAttribute("href", paths.home);
        await expect
            .element(screen.getByText("Review queue"))
            .not.toBeInTheDocument();
    });

    it("renders children for a caller who may review", async () => {
        roles.roles = ["WORKER"];
        roles.canReview = true;

        const screen = await renderGuard();

        await expect
            .element(screen.getByText("Review queue"))
            .toBeInTheDocument();
    });
});
