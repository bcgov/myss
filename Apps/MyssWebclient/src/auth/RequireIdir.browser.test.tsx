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

import RequireIdir from "./RequireIdir";

function renderGuard(initialEntry: string = paths.admin) {
    return render(
        <MemoryRouter initialEntries={[initialEntry]}>
            <Routes>
                <Route path={paths.home} element={<p>Regular landing</p>} />
                <Route
                    path="/admin/*"
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
    });

    it("shows a loading status while the session resolves", async () => {
        session.isLoading = true;

        const screen = await renderGuard();

        await expect.element(screen.getByText("Loading…")).toBeInTheDocument();
        expect(session.login).not.toHaveBeenCalled();
    });

    it("starts IDIR login once and preserves the exact admin destination", async () => {
        const destination = `${paths.adminFormManagement}?view=drafts#latest`;
        const screen = await renderGuard(destination);

        await expect
            .element(screen.getByText("Redirecting to IDIR sign in…"))
            .toBeInTheDocument();
        await vi.waitFor(() => {
            expect(session.login).toHaveBeenCalledOnce();
        });
        expect(session.login).toHaveBeenCalledWith("idir", destination);
    });

    it("redirects an authenticated non-IDIR user to home", async () => {
        session.isAuthenticated = true;
        session.user = { sub: "bceid-user", roles: [] };

        const screen = await renderGuard();

        await expect
            .element(screen.getByText("Regular landing"))
            .toBeInTheDocument();
    });

    it("renders children for an authenticated IDIR user", async () => {
        session.isAuthenticated = true;
        session.user = {
            sub: "idir-user",
            roles: [],
            idirUsername: "IDIRUSER",
        };

        const screen = await renderGuard();

        await expect
            .element(screen.getByText("Admin landing"))
            .toBeInTheDocument();
    });
});
