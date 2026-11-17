import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter } from "react-router";

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

import SignedOutPage from "./SignedOutPage";

describe("SignedOutPage", () => {
    beforeEach(() => {
        session.login.mockClear();
    });

    it("stays signed out: it never starts sign-in by itself", async () => {
        const screen = await render(
            <MemoryRouter initialEntries={[paths.signedOut]}>
                <SignedOutPage />
            </MemoryRouter>,
        );

        await expect
            .element(screen.getByRole("heading", { name: "You are signed out" }))
            .toBeInTheDocument();
        expect(session.login).not.toHaveBeenCalled();
    });

    it("signs in again with IDIR, back to the landing page, when asked", async () => {
        const screen = await render(
            <MemoryRouter initialEntries={[paths.signedOut]}>
                <SignedOutPage />
            </MemoryRouter>,
        );

        await screen.getByRole("button", { name: "Sign in with IDIR" }).click();

        expect(session.login).toHaveBeenCalledOnce();
        expect(session.login).toHaveBeenCalledWith(paths.home);
    });
});
