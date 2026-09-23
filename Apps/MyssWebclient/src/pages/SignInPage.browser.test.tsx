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
            isMeLoading: false,
            hasProfile: undefined,
            login: vi.fn(),
            logout: vi.fn(),
        }) as Session,
);

vi.mock("@/auth/useSession", () => ({
    useSession: () => session,
}));

import SignInPage from "./SignInPage";

function renderSignIn() {
    return render(
        <MemoryRouter initialEntries={[paths.signIn]}>
            <Routes>
                <Route path={paths.signIn} element={<SignInPage />} />
                <Route path={paths.dashboard} element={<p>Dashboard landing</p>} />
                <Route path={paths.register} element={<p>Registration landing</p>} />
            </Routes>
        </MemoryRouter>,
    );
}

describe("SignInPage", () => {
    beforeEach(() => {
        session.isAuthenticated = false;
        session.isMeLoading = false;
        session.hasProfile = undefined;
    });

    it("shows the identity-provider chooser to a signed-out visitor", async () => {
        const screen = await renderSignIn();

        await expect.element(screen.getByRole("heading", { name: "Sign in to My Self Serve" })).toBeInTheDocument();
        await expect.element(screen.getByRole("button", { name: "BC Services Card" })).toBeInTheDocument();
    });

    it("waits for the account lookup after authentication", async () => {
        session.isAuthenticated = true;
        session.isMeLoading = true;
        const screen = await renderSignIn();

        await expect
            .element(screen.getByRole("status", { name: "Checking your MySS account…" }))
            .toBeInTheDocument();
    });

    it("routes an authenticated user based on their profile status", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        const newUserScreen = await renderSignIn();
        await expect.element(newUserScreen.getByText("Registration landing")).toBeInTheDocument();

        session.hasProfile = true;
        const registeredUserScreen = await renderSignIn();
        await expect.element(registeredUserScreen.getByText("Dashboard landing")).toBeInTheDocument();
    });
});
