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
        }) as Session & {
            login: ReturnType<typeof vi.fn>;
            logout: ReturnType<typeof vi.fn>;
        },
);

vi.mock("@/auth/useSession", () => ({
    useSession: () => session,
}));

vi.mock("@/widgets/RegistrationForm", () => ({
    default: () => <p>Registration form</p>,
}));

import RegistrationPage from "./RegistrationPage";

function renderRegistration() {
    return render(
        <MemoryRouter initialEntries={[paths.register]}>
            <Routes>
                <Route path={paths.register} element={<RegistrationPage />} />
                <Route path={paths.dashboard} element={<p>Dashboard landing</p>} />
            </Routes>
        </MemoryRouter>,
    );
}

describe("RegistrationPage", () => {
    beforeEach(() => {
        session.user = undefined;
        session.isAuthenticated = false;
        session.isLoading = false;
        session.isMeLoading = false;
        session.hasProfile = undefined;
        session.login.mockClear();
        session.logout.mockClear();
    });

    it("offers both identity providers to a signed-out visitor", async () => {
        const screen = await renderRegistration();

        await expect
            .element(screen.getByText("Create a MySS account with your BC Services Card or BCeID."))
            .toBeInTheDocument();
        await screen.getByRole("button", { name: "BC Services Card" }).click();
        await screen.getByRole("button", { name: "BCeID" }).click();

        expect(session.login).toHaveBeenNthCalledWith(1, "bcServicesCard", paths.register);
        expect(session.login).toHaveBeenNthCalledWith(2, "bceid", paths.register);
    });

    it("shows the registration form for an authenticated user without a profile", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", name: "Ada", roles: [] };
        const screen = await renderRegistration();

        await expect.element(screen.getByText("Welcome, Ada.")).toBeInTheDocument();
        await expect.element(screen.getByText("Registration form")).toBeInTheDocument();
        await screen.getByRole("button", { name: "Log out" }).click();
        expect(session.logout).toHaveBeenCalledOnce();
    });

    it("redirects an authenticated user with a profile to the dashboard", async () => {
        session.isAuthenticated = true;
        session.hasProfile = true;
        const screen = await renderRegistration();

        await expect.element(screen.getByText("Dashboard landing")).toBeInTheDocument();
    });
});
