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

// The widget is mocked; its buttons stand in for the real form's outcomes.
vi.mock("@/widgets/RegistrationForm", () => ({
    default: (props: {
        identity: { givenName?: string };
        requirePin?: boolean;
        onRegistered: () => void;
        onCancel: () => void;
    }) => (
        <div>
            <p>Registration form for {props.identity.givenName ?? "nobody"}</p>
            <p>{props.requirePin ? "Asks for a PIN" : "No PIN"}</p>
            <button type="button" onClick={props.onRegistered}>
                Simulate registered
            </button>
            <button type="button" onClick={props.onCancel}>
                Simulate cancel
            </button>
        </div>
    ),
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
            .element(screen.getByRole("heading", { level: 1, name: "Create your MySS account" }))
            .toBeInTheDocument();
        await expect
            .element(screen.getByText(/you will need to first sign in with a B\.C\. government ID/))
            .toBeInTheDocument();
        await screen.getByRole("button", { name: "Register with BC Services Card" }).click();
        await screen.getByRole("button", { name: "Register with Basic BCeID" }).click();

        expect(session.login).toHaveBeenNthCalledWith(1, "bcServicesCard", paths.register);
        expect(session.login).toHaveBeenNthCalledWith(2, "bceid", paths.register);
    });

    it("offers account help to a signed-out visitor", async () => {
        const screen = await renderRegistration();

        await expect
            .element(screen.getByRole("heading", { level: 2, name: "MySS account Help" }))
            .toBeInTheDocument();
        await expect
            .element(screen.getByRole("button", { name: "Why do I need to log in with my government ID?" }))
            .toBeInTheDocument();
    });

    it("shows the signed-in banner and the form to an authenticated user without a profile", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", givenName: "Jane", roles: [] };
        const screen = await renderRegistration();

        await expect.element(screen.getByText("Signed in successfully")).toBeInTheDocument();
        await expect
            .element(screen.getByText(/You’re signed in with your BC Services Card\./))
            .toBeInTheDocument();
        await expect
            .element(screen.getByRole("heading", { level: 1, name: "Create your MySS account" }))
            .toBeInTheDocument();
        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
    });

    it("names BCeID in the banner for a BCeID sign-in", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", bceidGuid: "guid-1", roles: [] };
        const screen = await renderRegistration();

        await expect
            .element(screen.getByText(/You’re signed in with your BCeID\./))
            .toBeInTheDocument();
    });

    it("asks a BCeID citizen for a PIN and confirms it was created", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", bceidGuid: "guid-1", roles: [] };
        const screen = await renderRegistration();

        await expect.element(screen.getByText("Asks for a PIN")).toBeInTheDocument();
        await screen.getByRole("button", { name: "Simulate registered" }).click();

        await expect.element(screen.getByText("Your PIN has been created.")).toBeInTheDocument();
    });

    it("does not ask a BC Services Card citizen for a PIN", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", roles: [] };
        const screen = await renderRegistration();

        await expect.element(screen.getByText("No PIN")).toBeInTheDocument();
        await screen.getByRole("button", { name: "Simulate registered" }).click();

        await expect
            .element(screen.getByRole("heading", { level: 1, name: "Account registration complete" }))
            .toBeInTheDocument();
        await expect.element(screen.getByText("Your PIN has been created.")).not.toBeInTheDocument();
    });

    it("signs the user out when they cancel registration", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        const screen = await renderRegistration();

        await screen.getByRole("button", { name: "Simulate cancel" }).click();
        expect(session.logout).toHaveBeenCalledOnce();
    });

    // The refreshed /me reports a profile right after registering; the page
    // must keep the confirmation rather than redirect to the dashboard.
    it("shows the confirmation after registering, even once a profile exists", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        const screen = await renderRegistration();

        await screen.getByRole("button", { name: "Simulate registered" }).click();
        session.hasProfile = true;
        screen.rerender(
            <MemoryRouter initialEntries={[paths.register]}>
                <Routes>
                    <Route path={paths.register} element={<RegistrationPage />} />
                    <Route path={paths.dashboard} element={<p>Dashboard landing</p>} />
                </Routes>
            </MemoryRouter>,
        );

        const heading = screen.getByRole("heading", { level: 1, name: "Account registration complete" });
        await expect.element(heading).toHaveFocus();
        await expect
            .element(screen.getByText("Your account is being prepared. This should take less than 5 minutes."))
            .toBeInTheDocument();
        await expect.element(screen.getByText("Dashboard landing")).not.toBeInTheDocument();
    });

    it("returns to the homepage from the confirmation", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        const screen = await render(
            <MemoryRouter initialEntries={[paths.register]}>
                <Routes>
                    <Route path={paths.register} element={<RegistrationPage />} />
                    <Route path={paths.home} element={<p>Home landing</p>} />
                </Routes>
            </MemoryRouter>,
        );

        await screen.getByRole("button", { name: "Simulate registered" }).click();
        await screen.getByRole("button", { name: "Return to the MySS homepage" }).click();

        await expect.element(screen.getByText("Home landing")).toBeInTheDocument();
    });

    it("redirects an authenticated user with a profile to the dashboard", async () => {
        session.isAuthenticated = true;
        session.hasProfile = true;
        const screen = await renderRegistration();

        await expect.element(screen.getByText("Dashboard landing")).toBeInTheDocument();
    });
});
