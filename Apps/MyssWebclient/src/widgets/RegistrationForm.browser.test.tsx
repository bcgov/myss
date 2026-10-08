import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";

import { SubmissionRejectedError, type FormValidationError } from "@/api/forms";
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
        // As the real mutation's reset: the last refusal is forgotten.
        reset: vi.fn(() => {
            formState.submit.error = null;
        }),
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

function renderForm(
    overrides: Partial<IdentityDetails> | null = {},
    { requirePin = false }: { requirePin?: boolean } = {},
) {
    const onRegistered = vi.fn();
    const onCancel = vi.fn();
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
    });
    const details = overrides === null ? {} : { ...identity, ...overrides };
    // A fresh element each time: React skips re-rendering an identical one.
    const tree = () => (
        <QueryClientProvider client={queryClient}>
            <RegistrationForm
                identity={details}
                requirePin={requirePin}
                onRegistered={onRegistered}
                onCancel={onCancel}
            />
        </QueryClientProvider>
    );
    const screen = render(tree());
    // The mocked useSubmitForm does not re-render when its state changes.
    const rerender = async () => (await screen).rerender(tree());
    return { screen, rerender, onRegistered, onCancel };
}

/** A mutation the API refuses with these field errors, as useSubmitForm reports it. */
function refuseWith(errors: FormValidationError[]) {
    formState.submit.mutate.mockImplementation(
        (_input: unknown, callbacks?: { onError?: (error: Error) => void }) => {
            const error = new SubmissionRejectedError(422, errors);
            formState.submit.error = error;
            callbacks?.onError?.(error);
        },
    );
}

const sinRefused: FormValidationError = {
    field: "sin",
    keyword: "IDA.SIN.INVALID_CHECKSUM",
    message: "That Social Insurance Number is not valid.",
};

describe("RegistrationForm", () => {
    beforeEach(() => {
        formState.spec.data = undefined;
        formState.spec.error = null;
        formState.spec.isPending = false;
        formState.submit.data = undefined;
        formState.submit.error = null;
        formState.submit.isPending = false;
        formState.submit.mutate.mockReset();
        formState.submit.reset.mockClear();
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

    it("sends one registration when Complete registration is pressed twice", async () => {
        // The mocked mutation never settles and its isPending stays false, so
        // only a guard that does not wait for a re-render stops the second one.
        formState.spec.data = { version: 4, spec: specV4 };
        const screen = await renderForm().screen;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        const complete = screen.getByRole("button", { name: "Complete registration" });
        await complete.click();
        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        await complete.click();

        // Give a second Form.io submit time to arrive if it were going to.
        await new Promise((resolve) => setTimeout(resolve, 300));
        expect(formState.submit.mutate).toHaveBeenCalledOnce();
    });

    it("lets the citizen try again after the API refused", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        formState.submit.mutate.mockImplementation(
            (_input: unknown, callbacks?: { onError?: () => void }) => callbacks?.onError?.(),
        );
        const screen = await renderForm().screen;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        const complete = screen.getByRole("button", { name: "Complete registration" });
        await complete.click();
        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        await complete.click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledTimes(2));
    });

    it("puts the API's field errors on the field itself and focuses it", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        refuseWith([sinRefused]);
        const { screen: pending, rerender } = renderForm();
        const screen = await pending;

        const sin = screen.getByRole("textbox", { name: /Social Insurance Number/ });
        await sin.fill("050082833");
        await screen.getByRole("button", { name: "Complete registration" }).click();
        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        await rerender();

        // Focus goes to the first field in error, as in the other Form.io
        // widgets; the message is in the summary and under the field.
        await expect.element(sin).toHaveFocus();
        await expect
            .element(screen.getByRole("heading", { name: "There is a problem" }))
            .toBeVisible();
        await vi.waitFor(() =>
            expect(screen.getByText(sinRefused.message).elements()).toHaveLength(2),
        );
    });

    it("takes a field's API error off the field once it is changed", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        refuseWith([sinRefused]);
        const { screen: pending, rerender } = renderForm();
        const screen = await pending;

        const sin = screen.getByRole("textbox", { name: /Social Insurance Number/ });
        const complete = screen.getByRole("button", { name: "Complete registration" });
        await sin.fill("050082833");
        await complete.click();
        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        await rerender();
        await vi.waitFor(() =>
            expect(screen.getByText(sinRefused.message).elements()).toHaveLength(2),
        );

        // Off the field at once; the summary keeps it until the next submit.
        await sin.fill("046454286");
        await vi.waitFor(() =>
            expect(screen.getByText(sinRefused.message).elements()).toHaveLength(1),
        );
        await complete.click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledTimes(2));
    });

    it("leads from the summary to the radio group for an API error on gender", async () => {
        formState.spec.data = { version: 4, spec: specV4 };
        refuseWith([
            {
                field: "gender",
                keyword: "REGISTRATION.GENDER.UNKNOWN",
                message: "Choose one of the listed options.",
            },
        ]);
        const { screen: pending, rerender } = renderForm({ gender: undefined });
        const screen = await pending;

        await screen.getByText("Woman/Girl").click();
        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await screen.getByRole("button", { name: "Complete registration" }).click();
        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        await rerender();

        // The radio inputs have generated names, so this is the case the
        // summary's fallback to Form.io's component wrapper exists for.
        await screen
            .getByRole("button", { name: "Choose one of the listed options." })
            .click();
        await vi.waitFor(() =>
            expect(document.activeElement?.getAttribute("type")).toBe("radio"),
        );
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

describe("RegistrationForm PIN (MYSS-258)", () => {
    beforeEach(() => {
        formState.spec.data = { version: 4, spec: specV4 };
        formState.spec.error = null;
        formState.spec.isPending = false;
        formState.submit.error = null;
        formState.submit.isPending = false;
        formState.submit.mutate.mockReset();
        formState.submit.reset.mockClear();
    });

    // Masked inputs have no textbox role; their labels end in "(required)".
    type Screen = Awaited<ReturnType<typeof renderForm>["screen"]>;
    const pinInput = (screen: Screen) => screen.getByLabelText(/^Type in a PIN/);
    const confirmInput = (screen: Screen) => screen.getByLabelText(/^Re-type in the PIN/);

    function sentInput() {
        return formState.submit.mutate.mock.calls[0][0] as {
            answers: Record<string, unknown>;
            pin?: string;
            pinConfirmation?: string;
        };
    }

    it("is not asked of a citizen whose sign-in has no PIN", async () => {
        formState.submit.mutate.mockImplementation(
            (_input: unknown, callbacks?: { onSuccess?: () => void }) => callbacks?.onSuccess?.(),
        );
        const screen = await renderForm().screen;

        expect(
            screen.getByRole("heading", { name: /Create your Personal Identification Number/ }).query(),
        ).toBeNull();
        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        expect(sentInput()).not.toHaveProperty("pin");
        expect(sentInput()).not.toHaveProperty("pinConfirmation");
    });

    it("asks a BCeID citizen for a 4-digit PIN, twice", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;

        await expect
            .element(screen.getByRole("heading", { name: "Create your Personal Identification Number (PIN)" }))
            .toBeVisible();
        await expect.element(screen.getByText("To protect your identity, create a 4-digit PIN.")).toBeVisible();
        await expect.element(pinInput(screen)).toHaveAttribute("type", "password");
        await expect.element(pinInput(screen)).toHaveAttribute("inputmode", "numeric");
        await expect.element(confirmInput(screen)).toBeVisible();
    });

    it("sends the PIN beside the answers, never inside them", async () => {
        formState.submit.mutate.mockImplementation(
            (_input: unknown, callbacks?: { onSuccess?: () => void }) => callbacks?.onSuccess?.(),
        );
        const { screen: pending, onRegistered } = renderForm({}, { requirePin: true });
        const screen = await pending;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await pinInput(screen).fill("4821");
        await confirmInput(screen).fill("4821");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        expect(sentInput().pin).toBe("4821");
        expect(sentInput().pinConfirmation).toBe("4821");
        expect(JSON.stringify(sentInput().answers)).not.toContain("4821");
        expect(onRegistered).toHaveBeenCalledOnce();
    });

    it("does not send a PIN that is not typed twice the same", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await pinInput(screen).fill("4821");
        await confirmInput(screen).fill("4812");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await expect
            .element(screen.getByRole("heading", { name: "There is a problem" }))
            .toBeVisible();
        // In the summary and under the field.
        await vi.waitFor(() =>
            expect(screen.getByText("The two PINs do not match.").elements()).toHaveLength(2),
        );
        await new Promise((resolve) => setTimeout(resolve, 300));
        expect(formState.submit.mutate).not.toHaveBeenCalled();
    });

    it("lists the form's problems and the PIN's in one summary", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;

        // The SIN is required and left empty; the PIN is too short.
        await pinInput(screen).fill("48");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        const summary = screen.getByRole("alert");
        await expect.element(summary).toHaveTextContent(/Social Insurance Number/);
        await expect.element(summary).toHaveTextContent("Enter a 4-digit PIN using numbers only.");
        expect(formState.submit.mutate).not.toHaveBeenCalled();
    });

    it("leads from the summary to the PIN field", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await screen
            .getByRole("alert")
            .getByRole("button", { name: "Enter a 4-digit PIN using numbers only." })
            .click();
        await expect.element(pinInput(screen)).toHaveFocus();
    });

    it("takes the PIN's error off once it is changed", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await pinInput(screen).fill("4821");
        await confirmInput(screen).fill("4812");
        await screen.getByRole("button", { name: "Complete registration" }).click();
        await expect.element(confirmInput(screen)).toHaveAttribute("aria-invalid", "true");

        await confirmInput(screen).fill("4821");

        await expect.element(confirmInput(screen)).not.toHaveAttribute("aria-invalid");
    });

    it("puts the API's PIN error on the PIN field and focuses it", async () => {
        refuseWith([
            {
                field: "pinConfirmation",
                keyword: "IDA.PIN.MISMATCH",
                message: "The two PINs do not match.",
            },
        ]);
        const { screen: pending, rerender } = renderForm({}, { requirePin: true });
        const screen = await pending;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await pinInput(screen).fill("4821");
        await confirmInput(screen).fill("4821");
        await screen.getByRole("button", { name: "Complete registration" }).click();
        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        await rerender();

        await expect.element(confirmInput(screen)).toHaveFocus();
        await expect.element(confirmInput(screen)).toHaveAttribute("aria-invalid", "true");
    });
});
