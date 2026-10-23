import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import type { Session } from "@/auth/useSession";
import { paths } from "@/routes/paths";

const session = vi.hoisted(
    () =>
        ({
            user: undefined,
            isAuthenticated: false,
            isLoading: false,
            login: vi.fn(),
            logout: vi.fn(),
        }) as Session & { login: ReturnType<typeof vi.fn> },
);

vi.mock("@/auth/useSession", () => ({
    useSession: () => session,
}));

const logout = vi.hoisted(() => ({ inProgress: false }));

vi.mock("./siteMinderLogout", () => ({
    isLogoutInProgress: () => logout.inProgress,
}));

import RequireIdir from "./RequireIdir";

function renderGuard(initialEntry: string = paths.home) {
    return render(
        <MemoryRouter initialEntries={[initialEntry]}>
            <Routes>
                <Route
                    path="/*"
                    element={
                        <RequireIdir>
                            <p>Admin landing</p>
                        </RequireIdir>
                    }
                />
            </Routes>
        </MemoryRouter>,
    );
}

describe("RequireIdir", () => {
    beforeEach(() => {
        session.user = undefined;
        session.isAuthenticated = false;
        session.isLoading = false;
        session.login.mockClear();
        logout.inProgress = false;
    });

    it("starts no sign-in while a logout is under way", async () => {
        logout.inProgress = true;

        const screen = await renderGuard(paths.formManagement);

        await expect
            .element(screen.getByText("Signing you out…"))
            .toBeInTheDocument();
        expect(session.login).not.toHaveBeenCalled();
    });

    it("shows a loading status while the session resolves", async () => {
        session.isLoading = true;

        const screen = await renderGuard();

        await expect.element(screen.getByText("Loading…")).toBeInTheDocument();
        expect(session.login).not.toHaveBeenCalled();
    });

    it("starts IDIR login once and preserves the exact admin destination", async () => {
        const destination = `${paths.formManagement}?view=drafts#latest`;
        const screen = await renderGuard(destination);

        await expect
            .element(screen.getByText("Redirecting to IDIR sign in…"))
            .toBeInTheDocument();
        await vi.waitFor(() => {
            expect(session.login).toHaveBeenCalledOnce();
        });
        expect(session.login).toHaveBeenCalledWith(destination);
    });

    it("tells an authenticated non-IDIR user the site is for IDIR staff", async () => {
        session.isAuthenticated = true;
        session.user = { sub: "bceid-user" };

        const screen = await renderGuard();

        await expect
            .element(screen.getByRole("alert"))
            .toHaveTextContent(/signed in with IDIR/);
        await expect
            .element(screen.getByText("Admin landing"))
            .not.toBeInTheDocument();
    });

    it("renders children for an authenticated IDIR user", async () => {
        session.isAuthenticated = true;
        session.user = {
            sub: "idir-user",
            idirUsername: "IDIRUSER",
        };

        const screen = await renderGuard();

        await expect
            .element(screen.getByText("Admin landing"))
            .toBeInTheDocument();
    });
});
