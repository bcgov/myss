import { describe, it, expect, vi, beforeEach } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter } from "react-router";

import type { Session } from "@/auth/useSession";

const session = vi.hoisted(
    () =>
        ({
            user: undefined,
            isAuthenticated: false,
            isLoading: false,
            isMeLoading: false,
            login: vi.fn(),
            loginSilently: vi.fn(),
            logout: vi.fn(),
        }) as Session & { login: ReturnType<typeof vi.fn> },
);

vi.mock("@/auth/useSession", () => ({
    useSession: () => session,
}));

import RequireAuth from "./RequireAuth";

describe("RequireAuth", () => {
    beforeEach(() => {
        session.isAuthenticated = false;
        session.isLoading = false;
        session.login.mockClear();
    });

    it("shows a loading status while the session resolves", async () => {
        session.isLoading = true;
        const screen = await render(
            <MemoryRouter>
                <RequireAuth>
                    <p>secret</p>
                </RequireAuth>
            </MemoryRouter>,
        );
        await expect.element(screen.getByRole("status")).toBeInTheDocument();
    });

    it("shows the sign-in chooser when signed out", async () => {
        const screen = await render(
            <MemoryRouter>
                <RequireAuth>
                    <p>secret</p>
                </RequireAuth>
            </MemoryRouter>,
        );
        await expect
            .element(screen.getByRole("button", { name: "BC Services Card" }))
            .toBeInTheDocument();
    });

    it("renders children when authenticated", async () => {
        session.isAuthenticated = true;
        const screen = await render(
            <MemoryRouter>
                <RequireAuth>
                    <p>secret</p>
                </RequireAuth>
            </MemoryRouter>,
        );
        await expect.element(screen.getByText("secret")).toBeInTheDocument();
        await expect
            .element(screen.getByText("Checking your session…"))
            .not.toBeInTheDocument();
    });

    // MYSS-324: signing in from a guarded page comes back to it, query and all.
    it("signs in back to the page that asked", async () => {
        const screen = await render(
            <MemoryRouter initialEntries={["/account?pinReset=done"]}>
                <RequireAuth>
                    <p>secret</p>
                </RequireAuth>
            </MemoryRouter>,
        );

        await screen.getByRole("button", { name: "Basic BCeID" }).click();

        expect(session.login).toHaveBeenCalledWith(
            "bceid",
            "/account?pinReset=done",
        );
    });
});
