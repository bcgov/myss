import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render, type RenderResult } from "vitest-browser-react";
import { createMemoryRouter, MemoryRouter, Route, RouterProvider, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import type { Session } from "@/auth/useSession";
import { type AccountCreationAnswers, openingAnswers } from "@/lib/accountContext";
import { clearKept, keepAnswers, keepFile, readKeptAnswers, takeKeptFile } from "@/lib/keptAccountCreation";
import { paths } from "@/routes/paths";
import { loadPageStyles } from "@/testing/pageStyles";

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
        accountCreation: { request: unknown; representative: boolean };
        onAccountCreationRejected: (errors: { field: string; keyword: string; message: string }[]) => void;
        onRegistered: () => void;
        onCancel: () => void;
    }) => (
        <div>
            <h2 tabIndex={-1}>Step 1: Provide your information</h2>
            <p>Registration form for {props.identity.givenName ?? "nobody"}</p>
            <p>{props.requirePin ? "Asks for a PIN" : "No PIN"}</p>
            <p data-testid="account-creation">{JSON.stringify(props.accountCreation.request)}</p>
            <button type="button" onClick={props.onRegistered}>
                Simulate registered
            </button>
            <button type="button" onClick={props.onCancel}>
                Simulate cancel
            </button>
            <button
                type="button"
                onClick={() =>
                    props.onAccountCreationRejected([
                        {
                            field: "representativePhone",
                            keyword: "IDA.PHONE.INVALID_FORMAT",
                            message: "Phone number is invalid",
                        },
                    ])
                }
            >
                Simulate refused phone
            </button>
        </div>
    ),
}));

import RegistrationPage from "./RegistrationPage";

const SELF = "I’m making an account for myself (and family, if applicable)";
const LEGAL = "I’m a legal representative who is creating an account on behalf of someone else";

// The design system hides the real radio input; a person, and React Aria, use the label.
function choose(screen: RenderResult, label: string) {
    return screen.getByText(label, { exact: true }).click();
}

async function fillLegalRepresentative(screen: RenderResult) {
    await choose(screen, LEGAL);
    await screen.getByRole("textbox", { name: /^First name/ }).fill("Pat");
    await screen.getByRole("textbox", { name: /^Last name/ }).fill("Lee");
    await screen.getByRole("textbox", { name: /^Phone number/ }).fill("2505550123");
    await screen.getByRole("textbox", { name: "Agency (optional)" }).fill("Northern Trustee Services");
}

// The page's document control uploads through React Query, which the app provides at its root.
const queryClient = new QueryClient();

function withQueries(ui: React.ReactNode) {
    return <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>;
}

const SELF_PATH = "/registration?context=self";

/** The tab kept answers for Self before sign-in, so the form opens at once. */
function keptSelf() {
    keepAnswers(openingAnswers());
    return SELF_PATH;
}

function renderRegistration(path: string = paths.register) {
    return render(
        withQueries(
            <MemoryRouter initialEntries={[path]}>
                <Routes>
                    <Route path={paths.register} element={<RegistrationPage />} />
                    <Route path={paths.dashboard} element={<p>Dashboard landing</p>} />
                </Routes>
            </MemoryRouter>,
        ),
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

    afterEach(async () => {
        await clearKept();
        sessionStorage.clear();
    });

    it("asks a signed-out visitor who the account is for before any sign-in", async () => {
        const screen = await renderRegistration();

        await expect
            .element(screen.getByRole("heading", { level: 1, name: "Create your MySS account" }))
            .toBeInTheDocument();
        await expect.element(screen.getByRole("heading", { level: 2, name: "What’s needed" })).toBeInTheDocument();
        await expect
            .element(screen.getByRole("radiogroup", { name: "Creating a My Self Serve account" }))
            .toBeInTheDocument();
        await expect.element(screen.getByRole("radio", { name: SELF })).toBeChecked();
        await expect
            .element(screen.getByRole("button", { name: "Register with BC Services Card" }))
            .not.toBeInTheDocument();
    });

    it("offers both identity providers for Self, returning to the address that names it", async () => {
        const screen = await renderRegistration();

        await screen.getByRole("button", { name: "Continue" }).click();

        await expect.element(screen.getByRole("heading", { level: 1, name: "Create your MySS account" })).toHaveFocus();
        await expect.element(screen.getByText("Your answers")).not.toBeInTheDocument();
        await expect
            .element(screen.getByText(/you will need to first sign in with a B\.C\. government ID/))
            .toBeInTheDocument();
        await screen.getByRole("button", { name: "Register with BC Services Card" }).click();
        await screen.getByRole("button", { name: "Register with Basic BCeID" }).click();

        expect(session.login).toHaveBeenNthCalledWith(1, "bcServicesCard", "/registration?context=self");
        expect(session.login).toHaveBeenNthCalledWith(2, "bceid", "/registration?context=self");
    });

    it("offers account help with the sign-in options", async () => {
        const screen = await renderRegistration();
        await screen.getByRole("button", { name: "Continue" }).click();

        await expect
            .element(screen.getByRole("heading", { level: 2, name: "MySS account Help" }))
            .toBeInTheDocument();
        await expect
            .element(screen.getByRole("button", { name: "Why do I need to log in with my government ID?" }))
            .toBeInTheDocument();
    });

    it("lists a legal representative's missing details, stays on the questions, and links each to its field", async () => {
        const screen = await renderRegistration();
        await choose(screen, LEGAL);

        await screen.getByRole("button", { name: "Continue" }).click();

        await expect.element(screen.getByRole("heading", { name: "There is a problem" })).toHaveFocus();
        await expect.element(screen.getByRole("button", { name: "Enter your first name." })).toBeInTheDocument();
        await expect.element(screen.getByRole("button", { name: "Enter your last name." })).toBeInTheDocument();
        await expect.element(screen.getByRole("button", { name: "Enter your phone number." })).toBeInTheDocument();
        await expect.element(screen.getByRole("button", { name: "Register with Basic BCeID" })).not.toBeInTheDocument();

        await screen.getByRole("button", { name: "Enter your last name." }).click();
        await expect.element(screen.getByRole("textbox", { name: /^Last name/ })).toHaveFocus();
    });

    it("offers only Basic BCeID to a legal representative", async () => {
        const screen = await renderRegistration();
        await fillLegalRepresentative(screen);

        await screen.getByRole("button", { name: "Continue" }).click();

        await expect
            .element(screen.getByText(/To create an account for someone else, you will need to first sign in with Basic BCeID\./))
            .toBeInTheDocument();
        await expect
            .element(screen.getByRole("button", { name: "Register with BC Services Card" }))
            .not.toBeInTheDocument();
        await screen.getByRole("button", { name: "Register with Basic BCeID" }).click();
        expect(session.login).toHaveBeenCalledWith("bceid", "/registration?context=legal-representative");
    });

    it("returns to the questions with every answer kept, by Back", async () => {
        const router = createMemoryRouter(
            [{ path: paths.register, element: <RegistrationPage /> }],
            { initialEntries: [paths.register] },
        );
        const screen = await render(withQueries(<RouterProvider router={router} />));
        await fillLegalRepresentative(screen);
        await screen.getByRole("button", { name: "Continue" }).click();
        await expect.element(screen.getByRole("button", { name: "Register with Basic BCeID" })).toBeInTheDocument();

        await router.navigate(-1);

        await expect.element(screen.getByRole("radio", { name: LEGAL })).toBeChecked();
        // The sign-in page's heading has the same name, so this waits for the questions first.
        await expect
            .element(screen.getByRole("heading", { level: 1, name: "Create your MySS account" }))
            .toHaveFocus();
        await expect.element(screen.getByRole("textbox", { name: /^First name/ })).toHaveValue("Pat");
        await expect.element(screen.getByRole("textbox", { name: "Agency (optional)" })).toHaveValue(
            "Northern Trustee Services",
        );
    });

    it("asks the questions, with the address's context chosen, when the tab kept nothing", async () => {
        const screen = await renderRegistration("/registration?context=legal-representative");

        await expect.element(screen.getByRole("radio", { name: LEGAL })).toBeChecked();
        await expect
            .element(screen.getByRole("heading", { level: 3, name: "Enter your information as the authorized representative" }))
            .toBeInTheDocument();
        await expect.element(screen.getByRole("button", { name: "Register with Basic BCeID" })).not.toBeInTheDocument();
    });

    it("asks the questions for an address naming no known context", async () => {
        const screen = await renderRegistration("/registration?context=helper");

        await expect.element(screen.getByRole("radio", { name: SELF })).toBeChecked();
        await expect.element(screen.getByRole("button", { name: "Continue" })).toBeInTheDocument();
    });

    it("shows the signed-in banner and the form to an authenticated user without a profile", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", givenName: "Jane", roles: [] };
        const screen = await renderRegistration(keptSelf());

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

    it("names IDIR in the banner for an IDIR sign-in", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "worker", idirUsername: "WWORKER", roles: [] };
        const screen = await renderRegistration();

        await expect.element(screen.getByText(/You’re signed in with your IDIR\./)).toBeInTheDocument();
    });

    it("asks a BCeID citizen for a PIN and confirms it was created", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", bceidGuid: "guid-1", roles: [] };
        const screen = await renderRegistration(keptSelf());

        await expect.element(screen.getByText("Asks for a PIN")).toBeInTheDocument();
        await screen.getByRole("button", { name: "Simulate registered" }).click();

        await expect.element(screen.getByText("Your PIN has been created.")).toBeInTheDocument();
    });

    it("does not ask a BC Services Card citizen for a PIN", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.user = { sub: "new-user", roles: [] };
        const screen = await renderRegistration(keptSelf());

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
        const screen = await renderRegistration(keptSelf());

        await screen.getByRole("button", { name: "Simulate cancel" }).click();
        await expect.poll(() => session.logout.mock.calls.length).toBe(1);
        expect(readKeptAnswers()).toBeNull();
    });

    // The refreshed /me reports a profile right after registering; the page
    // must keep the confirmation rather than redirect to the dashboard.
    it("shows the confirmation after registering, even once a profile exists", async () => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        const screen = await renderRegistration(keptSelf());

        await screen.getByRole("button", { name: "Simulate registered" }).click();
        session.hasProfile = true;
        screen.rerender(
            withQueries(
                <MemoryRouter initialEntries={[SELF_PATH]}>
                    <Routes>
                        <Route path={paths.register} element={<RegistrationPage />} />
                        <Route path={paths.dashboard} element={<p>Dashboard landing</p>} />
                    </Routes>
                </MemoryRouter>,
            ),
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
            <MemoryRouter initialEntries={[keptSelf()]}>
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

describe("RegistrationPage after sign-in", () => {
    const HELPER = "I’m making an account for myself with help from someone else";
    const LEGAL = "I’m a legal representative who is creating an account on behalf of someone else";

    beforeEach(() => {
        session.isAuthenticated = true;
        session.hasProfile = false;
        session.isLoading = false;
        session.isMeLoading = false;
        session.user = { sub: "new-user", givenName: "Jane", bceidGuid: "guid-1", roles: [] };
        session.login.mockClear();
        session.logout.mockClear();
    });

    afterEach(async () => {
        vi.restoreAllMocks();
        await clearKept();
        sessionStorage.clear();
    });

    // Stores each upload, or refuses it as the API does.
    function stubUploads(refusal?: { status: number; keyword: string }) {
        const uploads: string[] = [];
        vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
            if (!String(input).endsWith("/v1/attachments") || init?.method !== "POST") {
                throw new Error(`Unexpected fetch in test: ${String(input)}`);
            }
            const file = (init.body as FormData).get("file") as File;
            uploads.push(file.name);
            if (refusal) {
                return new Response(JSON.stringify(refusal), {
                    status: refusal.status,
                    headers: { "Content-Type": "application/problem+json" },
                });
            }
            const stored = {
                id: "attachment-1",
                fileName: file.name,
                contentType: file.type,
                sizeBytes: file.size,
                status: "Released",
                uploadedAt: new Date().toISOString(),
            };
            return new Response(JSON.stringify({ payload: stored }), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            });
        });
        return uploads;
    }

    async function keptHelperWithConsent(): Promise<string> {
        const file = new File(["%PDF-1.7 consent"], "consent.pdf", { type: "application/pdf" });
        const id = await keepFile(file);
        const answers: AccountCreationAnswers = {
            ...openingAnswers(),
            context: "withHelper",
            helperAuthorized: "yes",
            consentDocuments: [{ id: id!, fileName: "consent.pdf", sizeBytes: file.size, state: "kept" }],
        };
        keepAnswers(answers);
        return "/registration?context=with-helper";
    }

    function keptLegalRepresentative(): string {
        keepAnswers({
            ...openingAnswers(),
            context: "legalRepresentative",
            representativeFirstName: "Pat",
            representativeLastName: "Lee",
            representativePhone: "2505550123",
        });
        return "/registration?context=legal-representative";
    }

    it("uploads the kept file, then opens the form with the account-creation data", async () => {
        const uploads = stubUploads();
        const screen = await renderRegistration(await keptHelperWithConsent());

        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
        expect(uploads).toEqual(["consent.pdf"]);
        expect(JSON.parse(screen.getByTestId("account-creation").element().textContent ?? "")).toEqual({
            context: "withHelper",
            helperAuthorized: true,
            consentDocumentIds: ["attachment-1"],
        });
        expect(readKeptAnswers()?.answers.consentDocuments).toEqual([
            { id: "attachment-1", fileName: "consent.pdf", sizeBytes: 16, state: "attached" },
        ]);
    });

    it("says it is attaching the documents while they upload", async () => {
        let release: () => void = () => {};
        const fetchSpy = vi.spyOn(window, "fetch").mockImplementation(
            () =>
                new Promise<Response>((resolve) => {
                    release = () =>
                        resolve(
                            new Response(
                                JSON.stringify({
                                    payload: { id: "a", fileName: "consent.pdf", contentType: "application/pdf", sizeBytes: 16, status: "Released", uploadedAt: "" },
                                }),
                                { status: 200, headers: { "Content-Type": "application/json" } },
                            ),
                        );
                }),
        );
        const screen = await renderRegistration(await keptHelperWithConsent());

        await expect.element(screen.getByRole("status")).toHaveTextContent("Attaching your documents… 0 of 1");
        await expect.poll(() => fetchSpy.mock.calls.length).toBe(1);
        release();
        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
    });

    it("asks again, with the context fixed and the file named, when an upload is refused", async () => {
        stubUploads({ status: 422, keyword: "DOC.UPLOAD.INFECTED" });
        const screen = await renderRegistration(await keptHelperWithConsent());

        await expect.element(screen.getByRole("alert")).toHaveTextContent(/A document was not attached/);
        await expect
            .element(screen.getByText("The file failed the virus scan and was not stored.", { exact: false }))
            .toBeInTheDocument();
        await expect.element(screen.getByText("Your answer:", { exact: false })).toBeInTheDocument();
        await expect.element(screen.getByText(HELPER)).toBeInTheDocument();
        await expect.element(screen.getByText("Registration form for Jane")).not.toBeInTheDocument();
    });

    it("opens the form for an account for oneself after Sign in, leaving what the tab kept alone", async () => {
        const uploads = stubUploads();
        await keptHelperWithConsent();
        const [kept] = readKeptAnswers()?.answers.consentDocuments ?? [];
        const screen = await renderRegistration();

        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
        expect(JSON.parse(screen.getByTestId("account-creation").element().textContent ?? "")).toEqual({
            context: "self",
        });
        await expect.element(screen.getByRole("heading", { name: "What’s needed" })).not.toBeInTheDocument();
        expect(screen.container.querySelector('[role="status"]')).toBeNull();
        expect(uploads).toEqual([]);
        expect(readKeptAnswers()?.answers.context).toBe("withHelper");
        expect((await takeKeptFile(kept.id))?.name).toBe("consent.pdf");
    });

    it("opens the form for a BC Services Card sign-in with no context, even with a representative's answers kept", async () => {
        session.user = { sub: "new-user", givenName: "Jane", roles: [] };
        keptLegalRepresentative();
        const screen = await renderRegistration();

        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
        await expect.element(screen.getByText("You signed in with a BC Services Card")).not.toBeInTheDocument();
    });

    it("opens the form for an address naming no known context", async () => {
        const screen = await renderRegistration("/registration?context=helper");

        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
        expect(JSON.parse(screen.getByTestId("account-creation").element().textContent ?? "")).toEqual({
            context: "self",
        });
    });

    it("asks again, offering what the sign-in allows, when the tab kept nothing for the address's context", async () => {
        session.user = { sub: "new-user", givenName: "Jane", roles: [] };
        const screen = await renderRegistration("/registration?context=with-helper");

        await expect.element(screen.getByRole("heading", { level: 2, name: "What’s needed" })).toBeInTheDocument();
        expect(screen.container.querySelectorAll('input[type="radio"]')).toHaveLength(2);
        await expect.element(screen.getByText(LEGAL)).not.toBeInTheDocument();

        await screen.getByRole("button", { name: "Continue" }).click();
        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
    });

    it("tells a BC Services Card sign-in that an account for someone else needs Basic BCeID", async () => {
        session.user = { sub: "new-user", givenName: "Jane", roles: [] };
        const screen = await renderRegistration(keptLegalRepresentative());

        await expect.element(screen.getByText("You signed in with a BC Services Card")).toBeInTheDocument();
        expect(screen.container.querySelectorAll('input[type="radio"]')).toHaveLength(2);
        await expect.poll(() => readKeptAnswers()).toBeNull();

        await screen.getByRole("button", { name: "Sign out" }).click();
        await expect.poll(() => session.logout.mock.calls.length).toBe(1);
    });

    it("opens the form at once when the tab kept answers with nothing to upload", async () => {
        const uploads = stubUploads();
        const screen = await renderRegistration(keptSelf());

        await expect.element(screen.getByText("Registration form for Jane")).toBeInTheDocument();
        expect(screen.container.querySelector('[role="status"]')).toBeNull();
        expect(uploads).toEqual([]);
    });

    it("asks again when the API refuses an answer, and returns to the same form", async () => {
        const screen = await renderRegistration(keptLegalRepresentative());
        await screen.getByRole("button", { name: "Simulate refused phone" }).click();

        await expect.element(screen.getByText("Your answer:", { exact: false })).toBeInTheDocument();
        await expect.element(screen.getByText("Registration form for Jane")).not.toBeVisible();
        await screen.getByRole("textbox", { name: "Agency (optional)" }).fill("Northern Trustee Services");
        await screen.getByRole("button", { name: "Continue" }).click();

        await expect.element(screen.getByText("Registration form for Jane")).toBeVisible();
        await expect
            .element(screen.getByRole("heading", { name: "Step 1: Provide your information" }))
            .toHaveFocus();
        expect(JSON.parse(screen.getByTestId("account-creation").element().textContent ?? "").representative).toEqual({
            firstName: "Pat",
            lastName: "Lee",
            phone: "2505550123",
            agency: "Northern Trustee Services",
        });
    });

    it("asks again with the API's message on the field it refused", async () => {
        const screen = await renderRegistration(keptLegalRepresentative());
        await screen.getByRole("button", { name: "Simulate refused phone" }).click();

        await expect.element(screen.getByRole("heading", { name: "There is a problem" })).toBeInTheDocument();
        await expect
            .element(screen.getByRole("textbox", { name: /^Phone number/ }))
            .toHaveAccessibleDescription(/Phone number is invalid/);
    });

    it("clears what the tab kept when the citizen cancels the questions", async () => {
        const screen = await renderRegistration(keptLegalRepresentative());
        await screen.getByRole("button", { name: "Simulate refused phone" }).click();

        await screen.getByRole("button", { name: "Cancel" }).click();

        await expect.poll(() => session.logout.mock.calls.length).toBe(1);
        expect(readKeptAnswers()).toBeNull();
    });

    it("asks for the documents again when the browser could not keep them", async () => {
        keepAnswers({ ...openingAnswers(), context: "withHelper", helperAuthorized: "yes" }, { filesNotKept: true });
        const screen = await renderRegistration("/registration?context=with-helper");

        await expect.element(screen.getByText("Attach your documents")).toBeInTheDocument();
        await expect.element(screen.getByText("Your answer:", { exact: false })).toBeInTheDocument();
        await expect.element(screen.getByRole("button", { name: "Attach document" })).toBeInTheDocument();
        await expect.element(screen.getByText("Registration form for Jane")).not.toBeInTheDocument();
    });

    it("uploads nothing for a citizen who already has an account, and forgets the answers", async () => {
        const uploads = stubUploads();
        session.hasProfile = true;
        const path = await keptHelperWithConsent();

        const screen = await renderRegistration(path);

        await expect.element(screen.getByText("Dashboard landing")).toBeInTheDocument();
        await expect.poll(() => readKeptAnswers()).toBeNull();
        expect(uploads).toEqual([]);
    });

    it("clears what the tab kept once the registration completes", async () => {
        const screen = await renderRegistration(keptSelf());

        await screen.getByRole("button", { name: "Simulate registered" }).click();

        await expect
            .element(screen.getByRole("heading", { level: 1, name: "Account registration complete" }))
            .toBeInTheDocument();
        await expect.poll(() => readKeptAnswers()).toBeNull();
    });
});

describe("RegistrationPage layout", () => {
    let initial = { width: 0, height: 0 };

    beforeAll(loadPageStyles);

    beforeEach(() => {
        session.user = undefined;
        session.isAuthenticated = false;
        session.hasProfile = undefined;
        initial = { width: window.innerWidth, height: window.innerHeight };
    });

    afterEach(async () => {
        await page.viewport(initial.width, initial.height);
    });

    it.each([
        ["32px", 320, 640],
        ["36px", 1280, 800],
    ])("sets the title to %s at %i pixels wide", async (size, width, height) => {
        await page.viewport(width, height);
        const screen = await renderRegistration();
        const title = screen.getByRole("heading", { level: 1, name: "Create your MySS account" });
        await expect.element(title).toBeVisible();

        expect(getComputedStyle(title.element()).fontSize).toBe(size);
        const root = document.documentElement;
        expect(root.scrollWidth).toBeLessThanOrEqual(root.clientWidth);
    });
});
