import { describe, it, expect, vi, beforeEach } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter } from "react-router";

import type { Session } from "@/auth/useSession";

// Mock the seam so the component test is independent of the OIDC backing.
const session = vi.hoisted(
    () =>
        ({
            user: undefined,
            isAuthenticated: false,
            isLoading: false,
            login: vi.fn(),
            logout: vi.fn(),
        }) as Session & { login: ReturnType<typeof vi.fn>; logout: ReturnType<typeof vi.fn> },
);

vi.mock("@/auth/useSession", () => ({
    useSession: () => session,
}));

import AccountPanel from "./AccountPanel";

function renderPanel() {
    return render(
        <MemoryRouter>
            <AccountPanel />
        </MemoryRouter>,
    );
}

describe("AccountPanel", () => {
    beforeEach(() => {
        session.isAuthenticated = false;
        session.user = undefined;
        session.login.mockClear();
        session.logout.mockClear();
    });

    it("shows sign in / create account when signed out", async () => {
        const screen = await renderPanel();
        await expect.element(screen.getByText("Sign in")).toBeInTheDocument();
        await expect
            .element(screen.getByText("Create an account"))
            .toBeInTheDocument();
    });

    it("shows a welcome and sign out when signed in", async () => {
        session.isAuthenticated = true;
        session.user = { sub: "u1", name: "Alice", roles: [] };
        const screen = await renderPanel();
        await expect
            .element(screen.getByText(/Welcome back, Alice/))
            .toBeInTheDocument();
        const signOut = screen.getByRole("button", { name: "Sign out" });
        await signOut.click();
        expect(session.logout).toHaveBeenCalled();
    });
});
