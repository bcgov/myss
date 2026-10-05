import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";

import type { IdentityDetails } from "./registrationSpec";
import { registerBcgovComponents } from "./formio/bcgovComponents";

const formState = vi.hoisted(() => ({
    spec: {
        data: undefined as { title?: string; version: number; spec: object } | undefined,
        error: null as Error | null,
        isPending: false,
    },
    submit: {
        data: undefined as object | undefined,
        error: null as Error | null,
        isPending: false,
        mutate: vi.fn(),
    },
}));

vi.mock("@/hooks/usePocForm", () => ({
    useFormSpec: () => formState.spec,
    useSubmitForm: () => formState.submit,
}));

import RegistrationForm from "./RegistrationForm";

registerBcgovComponents();

// The shape of registration v4 in MyssContent's seed data, trimmed to what the
// widget acts on: prefill markers, a BC Gov radio and no submit button.
const specV4 = {
    display: "form",
    components: [
        {
            type: "textfield",
            key: "firstName",
            label: "First name",
            input: true,
            validate: { required: true },
            properties: { myssPrefill: "givenName", myssPrefillLock: "true" },
        },
        {
            type: "textfield",
            key: "lastName",
            label: "Last name",
            input: true,
            validate: { required: true },
            properties: { myssPrefill: "familyName", myssPrefillLock: "true" },
        },
        {
            type: "email",
            key: "email",
            label: "Email",
            input: true,
            validate: { required: true },
            properties: { myssPrefill: "email" },
        },
        {
            type: "bcgovRadio",
            key: "gender",
            label: "Gender",
            input: true,
            values: [
                { label: "Man/Boy", value: "man" },
                { label: "Woman/Girl", value: "woman" },
            ],
            validate: { required: true },
            properties: { myssPrefill: "gender", myssPrefillLock: "true" },
        },
        {
            type: "textfield",
            key: "sin",
            label: "Social Insurance Number (SIN)",
            input: true,
            validate: { required: true },
        },
    ],
};

const identity: IdentityDetails = {
    givenName: "Jane",
    familyName: "Johnson",
    email: "jane@example.com",
    gender: "female",
};

function renderForm(overrides: Partial<IdentityDetails> | null = {}) {
    const onRegistered = vi.fn();
    const onCancel = vi.fn();
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
    });
    const screen = render(
        <QueryClientProvider client={queryClient}>
            <RegistrationForm
                identity={overrides === null ? {} : { ...identity, ...overrides }}
                onRegistered={onRegistered}
                onCancel={onCancel}
            />
        </QueryClientProvider>,
    );
    return { screen, onRegistered, onCancel };
}

describe("RegistrationForm", () => {
    beforeEach(() => {
        formState.spec.data = undefined;
        formState.spec.error = null;
        formState.spec.isPending = false;
        formState.submit.data = undefined;
        formState.submit.error = null;
        formState.submit.isPending = false;
        formState.submit.mutate.mockReset();
    });

    it("shows loading while the registration spec is fetched", async () => {
        formState.spec.isPending = true;
        const { screen } = renderForm();

        await expect.element((await screen).getByText("Loading form…")).toBeInTheDocument();
    });

    it("shows the spec failure", async () => {
        formState.spec.error = new Error("Spec fetch failed (404)");
        const { screen } = renderForm();

        await expect
            .element((await screen).getByText("Could not load the form: Spec fetch failed (404)"))
            .toBeInTheDocument();
    });

    it("prefills from the identity and locks only the fields the spec locks", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        const screen = await renderForm().screen;

        const firstName = screen.getByRole("textbox", { name: /First name/ });
        await expect.element(firstName).toHaveValue("Jane");
        await expect.element(firstName).toBeDisabled();
        await expect.element(screen.getByRole("textbox", { name: /Last name/ })).toBeDisabled();

        const email = screen.getByRole("textbox", { name: /Email/ });
        await expect.element(email).toHaveValue("jane@example.com");
        await expect.element(email).toBeEnabled();

        const woman = screen.getByRole("radio", { name: "Woman/Girl" });
        await expect.element(woman).toBeChecked();
        await expect.element(woman).toBeDisabled();
    });

    it("leaves a lockable field editable when the identity did not supply it", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        const screen = await renderForm(null).screen;

        await expect.element(screen.getByRole("textbox", { name: /First name/ })).toBeEnabled();
        await expect.element(screen.getByRole("radio", { name: "Man/Boy" })).toBeEnabled();
    });

    // v3, the version published today, still carries its own Form.io submit
    // button; the widget's BC Gov buttons replace it.
    it("replaces the spec's submit button with BC Gov Cancel and Complete registration", async () => {
        formState.spec.data = {
            version: 3,
            spec: {
                display: "form",
                components: [
                    { type: "textfield", key: "firstName", label: "First name", input: true },
                    { type: "button", key: "submit", action: "submit", label: "Submit", input: true },
                ],
            },
        };
        const { screen: pending, onCancel } = renderForm();
        const screen = await pending;

        await expect.element(screen.getByRole("textbox", { name: /First name/ })).toBeInTheDocument();
        expect(screen.getByRole("button", { name: "Submit" }).elements()).toHaveLength(0);

        await screen.getByRole("button", { name: "Cancel" }).click();
        expect(onCancel).toHaveBeenCalledOnce();
    });

    it("submits the locked values with the rest and reports the registration", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        formState.submit.mutate.mockImplementation(
            (_input: unknown, callbacks?: { onSuccess?: () => void }) => callbacks?.onSuccess?.(),
        );
        const { screen: pending, onRegistered } = renderForm();
        const screen = await pending;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        const [input] = formState.submit.mutate.mock.calls[0] as [
            { formSpecVersion: number; answers: Record<string, unknown> },
        ];
        expect(input.formSpecVersion).toBe(4);
        expect(input.answers).toMatchObject({
            firstName: "Jane",
            lastName: "Johnson",
            email: "jane@example.com",
            gender: "woman",
            sin: "050082833",
        });
        expect(onRegistered).toHaveBeenCalledOnce();
    });

    it("does not report a registration the API refused", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        formState.submit.mutate.mockImplementation(
            (_input: unknown, callbacks?: { onError?: () => void }) => callbacks?.onError?.(),
        );
        const { screen: pending, onRegistered } = renderForm();
        const screen = await pending;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        expect(onRegistered).not.toHaveBeenCalled();
        await expect
            .element(screen.getByRole("button", { name: "Complete registration" }))
            .toBeEnabled();
    });

    it("does not submit while a required field is empty", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        const screen = await renderForm().screen;

        await expect.element(screen.getByRole("textbox", { name: /First name/ })).toHaveValue("Jane");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        // Give Form.io's validation pass time to run and refuse.
        await new Promise((resolve) => setTimeout(resolve, 300));
        expect(formState.submit.mutate).not.toHaveBeenCalled();
    });
});
