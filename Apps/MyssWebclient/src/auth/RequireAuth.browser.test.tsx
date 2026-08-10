import { describe, it, expect, vi, beforeEach } from "vitest";
import { render } from "vitest-browser-react";

import type { Session } from "@/auth/useSession";

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
            <RequireAuth>
                <p>secret</p>
            </RequireAuth>,
        );
        await expect.element(screen.getByRole("status")).toBeInTheDocument();
    });

    it("shows the sign-in chooser when signed out", async () => {
        const screen = await render(
            <RequireAuth>
                <p>secret</p>
            </RequireAuth>,
        );
        await expect
            .element(screen.getByRole("button", { name: "BC Services Card" }))
            .toBeInTheDocument();
    });

    it("renders children when authenticated", async () => {
        session.isAuthenticated = true;
        const screen = await render(
            <RequireAuth>
                <p>secret</p>
            </RequireAuth>,
        );
        await expect.element(screen.getByText("secret")).toBeInTheDocument();
    });
});
