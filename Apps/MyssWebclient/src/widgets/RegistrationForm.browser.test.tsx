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

const specV5 = {
    ...specV4,
    components: [
        ...specV4.components.slice(0, 3),
        {
            type: "textfield",
            key: "phone",
            label: "Phone number",
            input: true,
            placeholder: "(250) 555-0199",
            validateOn: "blur",
            validate: { required: true, customMessage: "Phone number is invalid" },
            properties: {
                myssValidator: "phone",
                myssPrefill: "phoneNumber",
                myssPrefillLock: "true",
            },
        },
        ...specV4.components.slice(3),
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
    const details = overrides === null ? {} : { ...identity, ...overrides };
    // A fresh element each time: React skips re-rendering an identical one.
    const tree = () => (
        <QueryClientProvider client={queryClient}>
            <RegistrationForm
                identity={details}
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
        (_input: unknown, callbacks?: { onError?: () => void }) => {
            formState.submit.error = new SubmissionRejectedError(422, errors);
            callbacks?.onError?.();
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

    it("validates a v5 phone on blur without masking an overlong number", async () => {
        formState.spec.data = { version: 5, spec: specV5 };
        const screen = await renderForm({ phoneNumber: "+1 250 555 0199" }).screen;
        const phone = screen.getByRole("textbox", { name: "Phone number" });
        const email = screen.getByRole("textbox", { name: /Email/ });

        await expect.element(phone).toBeEnabled();
        await expect.element(phone).toHaveValue("");
        await phone.fill("25055501999");
        await expect.element(phone).toHaveValue("25055501999");
        await email.click();
        await expect.element(screen.getByText("Phone number is invalid").first()).toBeVisible();

        await phone.fill("(250) 555-0199");
        await email.click();
        await expect.element(screen.getByText("Phone number is invalid")).not.toBeInTheDocument();

        await phone.fill("");
        await email.click();
        await expect.element(screen.getByText("Phone number is invalid").first()).toBeVisible();
    });

    it("locks a valid v5 phone supplied by the identity", async () => {
        formState.spec.data = { version: 5, spec: specV5 };
        const screen = await renderForm({ phoneNumber: "250 555 0199" }).screen;
        const phone = screen.getByRole("textbox", { name: "Phone number" });

        await expect.element(phone).toHaveValue("(250) 555-0199");
        await expect.element(phone).toBeDisabled();
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
