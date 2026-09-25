import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import type { Session } from "@/auth/useSession";
import { paths } from "@/routes/paths";

const session = vi.hoisted(
    () =>
        ({
            user: undefined,
            isAuthenticated: true,
            isLoading: false,
            isMeLoading: false,
            hasProfile: true,
            profileFirstName: null,
            login: vi.fn(),
            logout: vi.fn(),
        }) as Session & { logout: ReturnType<typeof vi.fn> },
);

vi.mock("@/auth/useSession", () => ({
    useSession: () => session,
}));

import DashboardPage from "./DashboardPage";

function renderDashboard() {
    return render(
        <MemoryRouter initialEntries={[paths.dashboard]}>
            <Routes>
                <Route path={paths.dashboard} element={<DashboardPage />} />
                <Route path={paths.register} element={<p>Registration landing</p>} />
            </Routes>
        </MemoryRouter>,
    );
}

describe("DashboardPage", () => {
    beforeEach(() => {
        session.user = { sub: "user-1", name: "Alice", roles: [] };
        session.isMeLoading = false;
        session.hasProfile = true;
        session.profileFirstName = null;
        session.logout.mockClear();
    });

    it("shows account checking while the profile lookup is pending", async () => {
        session.isMeLoading = true;
        const screen = await renderDashboard();

        await expect.element(screen.getByRole("status")).toHaveTextContent(
            "Checking your MySS account…",
        );
    });

    it("redirects an authenticated user without a profile to registration", async () => {
        session.hasProfile = false;
        const screen = await renderDashboard();

        await expect.element(screen.getByText("Registration landing")).toBeInTheDocument();
    });

    it("shows profile information and signs out a registered user", async () => {
        session.profileFirstName = "Ada";
        const screen = await renderDashboard();

        await expect.element(screen.getByRole("heading", { name: "Hello Alice" })).toBeInTheDocument();
        await expect
            .element(screen.getByText("Your MySS account profile is registered to Ada."))
            .toBeInTheDocument();

        await screen.getByRole("button", { name: "Log out" }).click();
        expect(session.logout).toHaveBeenCalledOnce();
    });
});
