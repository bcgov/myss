import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";

import { AccountRequestError } from "@/api/account";
import { SubmissionRejectedError, type FormValidationError } from "@/api/forms";
import type { IdentityDetails } from "./registrationSpec";
import { registerBcgovComponents } from "@myss/formio";

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
    // EmailVerification off unless a test turns it on: most tests here are
    // about the form, and go from Step 1 straight to the review.
    features: {} as Record<string, boolean | undefined>,
    requestEmail: {
        mutate: vi.fn(),
        reset: vi.fn(),
        isPending: false,
        isSuccess: false,
        error: null as Error | null,
    },
    emailStatus: {
        data: undefined as { email?: string | null; verified: boolean } | undefined,
        refetch: vi.fn(),
    },
}));

vi.mock("@/hooks/usePocForm", () => ({
    useFormSpec: () => formState.spec,
    useSubmitForm: () => formState.submit,
}));
vi.mock("@/hooks/useFeatures", () => ({
    // A name set to undefined is still loading; one not set at all is off.
    useFeature: (name: string) =>
        name in formState.features ? formState.features[name] : false,
}));
vi.mock("@/hooks/useAccount", async (importOriginal) => ({
    ...(await importOriginal<typeof import("@/hooks/useAccount")>()),
    useRequestRegistrationEmail: () => formState.requestEmail,
    useRegistrationEmail: () => formState.emailStatus,
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

/**
 * Step 1's Continue, then the review (MYSS-295). With EmailVerification off,
 * as most tests here have it, there is no Step 2 in between.
 */
async function continueToReview(screen: Awaited<ReturnType<typeof renderForm>["screen"]>) {
    await screen.getByRole("button", { name: "Continue" }).click();
    await expect
        .element(screen.getByRole("heading", { name: /Review and submit/ }))
        .toBeVisible();
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
        formState.features = {};
        formState.requestEmail.mutate.mockReset();
        formState.requestEmail.isSuccess = false;
        formState.requestEmail.error = null;
        formState.emailStatus.data = undefined;
        formState.emailStatus.refetch.mockReset();
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

    it("draws the phone like every other field, and the consent on the review", async () => {
        formState.spec.data = {
            version: 4,
            spec: {
                display: "form",
                components: [
                    { type: "textfield", key: "firstName", label: "First name", input: true },
                    { type: "phoneNumber", key: "phone", label: "Phone", input: true },
                    {
                        type: "checkbox",
                        key: "consent",
                        label: "I accept the Terms of Use.",
                        input: true,
                        validate: { required: true },
                    },
                ],
            },
        };
        const screen = await renderForm().screen;

        // The consent is asked for on the review step (MYSS-295), not here.
        await expect.element(screen.getByRole("textbox", { name: /First name/ })).toBeInTheDocument();
        expect(document.querySelector(".bcds-react-aria-Checkbox")).toBeNull();

        // phoneNumber is a design system telephone input now, not Form.io's
        // own markup with its red star and "numeric only" hint.
        await expect.element(screen.getByRole("textbox", { name: /Phone/ })).toBeInTheDocument();
        const phone = document.querySelector<HTMLInputElement>('input[type="tel"]');
        expect(phone, "the phone field renders as a telephone input").not.toBeNull();
        expect(phone?.closest(".bcds-react-aria-TextField")).not.toBeNull();
        expect(document.querySelector(".form-control, .required-star")).toBeNull();

        // On the review step it is a design system checkbox, lined up with
        // the step's other content rather than indented.
        await screen.getByRole("textbox", { name: /Phone/ }).fill("2505550199");
        await continueToReview(screen);
        const checkbox = document.querySelector(".bcds-react-aria-Checkbox");
        expect(checkbox).not.toBeNull();
        const heading = screen.getByRole("heading", { name: /Review and submit/ }).element();
        expect(Math.round(checkbox!.getBoundingClientRect().left)).toBe(
            Math.round(heading.getBoundingClientRect().left),
        );
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
    it("replaces the spec's submit button with BC Gov Cancel and Continue", async () => {
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
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await screen.getByRole("button", { name: "Continue" }).click();

        // Give Form.io's validation pass time to run and refuse.
        await new Promise((resolve) => setTimeout(resolve, 300));
        await expect
            .element(screen.getByRole("heading", { name: "Step 1: Provide your information" }))
            .toBeVisible();
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
        formState.features = {};
        formState.requestEmail.mutate.mockReset();
        formState.requestEmail.isSuccess = false;
        formState.requestEmail.error = null;
        formState.emailStatus.data = undefined;
        formState.emailStatus.refetch.mockReset();
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
        await continueToReview(screen);
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        expect(sentInput()).not.toHaveProperty("pin");
        expect(sentInput()).not.toHaveProperty("pinConfirmation");
    });

    it("asks a BCeID citizen for a 4-digit PIN, twice, on the review step", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;
        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await continueToReview(screen);

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
        await continueToReview(screen);
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
        await continueToReview(screen);
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

    it("holds Step 1 back on its own problems, and the review on the PIN's", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;

        // The SIN is required and left empty: Step 1 does not let go.
        await screen.getByRole("button", { name: "Continue" }).click();
        await expect.element(screen.getByRole("alert")).toHaveTextContent(/Social Insurance Number/);

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await continueToReview(screen);
        await pinInput(screen).fill("48");
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await expect
            .element(screen.getByRole("alert"))
            .toHaveTextContent("Enter a 4-digit PIN using numbers only.");
        expect(formState.submit.mutate).not.toHaveBeenCalled();
    });

    it("leads from the summary to the PIN field", async () => {
        const screen = await renderForm({}, { requirePin: true }).screen;

        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await continueToReview(screen);
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
        await continueToReview(screen);
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
        await continueToReview(screen);
        await pinInput(screen).fill("4821");
        await confirmInput(screen).fill("4821");
        await screen.getByRole("button", { name: "Complete registration" }).click();
        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        await rerender();

        await expect.element(confirmInput(screen)).toHaveFocus();
        await expect.element(confirmInput(screen)).toHaveAttribute("aria-invalid", "true");
    });
});

describe("RegistrationForm steps (MYSS-295)", () => {
    beforeEach(() => {
        formState.spec.data = { version: 4, spec: specV4 };
        formState.spec.error = null;
        formState.spec.isPending = false;
        formState.submit.error = null;
        formState.submit.isPending = false;
        formState.submit.mutate.mockReset();
        formState.submit.reset.mockClear();
        formState.features = { EmailVerification: true };
        formState.requestEmail.mutate.mockReset();
        formState.requestEmail.isSuccess = false;
        formState.requestEmail.error = null;
        formState.emailStatus.data = undefined;
        formState.emailStatus.refetch.mockReset();
    });

    type Screen = Awaited<ReturnType<typeof renderForm>["screen"]>;

    /** The link request answers as the API would. */
    function emailAnswers(status: { email: string; verified: boolean }) {
        formState.requestEmail.mutate.mockImplementation(
            (_email: string, callbacks?: { onSuccess?: (s: typeof status) => void }) =>
                callbacks?.onSuccess?.(status),
        );
    }

    /** Step 2's own buttons: Step 1's, hidden but mounted, share the name. */
    const step2 = (screen: Screen) =>
        screen.getByRole("region", { name: "Step 2: Verify your email" });

    async function fillAndContinue(screen: Screen) {
        await screen.getByRole("textbox", { name: /Social Insurance Number/ }).fill("050082833");
        await screen.getByRole("button", { name: "Continue" }).click();
    }

    it("waits for the feature flags before Step 1 can continue", async () => {
        formState.features = { EmailVerification: undefined };
        const screen = await renderForm().screen;

        await expect.element(screen.getByRole("button", { name: "Continue" })).toBeDisabled();
    });

    it("shows the steps, the current one marked", async () => {
        const screen = await renderForm().screen;

        const current = screen.getByRole("listitem").filter({ hasText: "Provide your information" });
        await expect.element(current).toHaveAttribute("aria-current", "step");
        await expect.element(screen.getByText("Verify your email")).toBeVisible();
    });

    it("sends the link to the Step 1 address and opens Step 2 with it", async () => {
        emailAnswers({ email: "jane@example.com", verified: false });
        const screen = await renderForm().screen;

        await fillAndContinue(screen);

        // Form.io validates before it submits, so the request arrives a moment later.
        await vi.waitFor(() =>
            expect(formState.requestEmail.mutate.mock.calls[0]?.[0]).toBe("jane@example.com"),
        );
        await expect
            .element(screen.getByRole("heading", { name: "Step 2: Verify your email" }))
            .toBeVisible();
        await expect.element(screen.getByText("jane@example.com").first()).toBeVisible();
    });

    it("goes straight to the review for an address already proven (AC6)", async () => {
        emailAnswers({ email: "jane@example.com", verified: true });
        const screen = await renderForm().screen;

        await fillAndContinue(screen);

        await expect
            .element(screen.getByRole("heading", { name: "Step 3: Review and submit" }))
            .toBeVisible();
    });

    it("will not continue until the address is verified, then does (AC4, AC5)", async () => {
        emailAnswers({ email: "jane@example.com", verified: false });
        formState.emailStatus.refetch.mockResolvedValueOnce({
            data: { email: "jane@example.com", verified: false },
        });
        const screen = await renderForm().screen;
        await fillAndContinue(screen);

        await step2(screen).getByRole("button", { name: "Continue" }).click();
        await expect.element(screen.getByText("Email verification is required")).toBeVisible();

        formState.emailStatus.refetch.mockResolvedValueOnce({
            data: { email: "jane@example.com", verified: true },
        });
        await step2(screen).getByRole("button", { name: "Continue" }).click();
        await expect
            .element(screen.getByRole("heading", { name: "Step 3: Review and submit" }))
            .toBeVisible();
    });

    // MYSS-295 retest, 2026-10-28: an API without the endpoint answered 404,
    // and the form took that for "verification is off" and skipped Step 2,
    // which the stepper then showed as done. It is a failure, and says so.
    it("does not skip Step 2 when the link cannot be sent", async () => {
        formState.requestEmail.mutate.mockImplementation(
            (_email: string, callbacks?: { onError?: (e: Error) => void }) =>
                callbacks?.onError?.(new AccountRequestError(404, [])),
        );
        const screen = await renderForm().screen;

        await fillAndContinue(screen);

        await expect
            .element(screen.getByText(/We could not send the verification email/))
            .toBeVisible();
        await expect
            .element(screen.getByRole("heading", { name: "Step 1: Provide your information" }))
            .toBeVisible();
        expect(screen.getByRole("heading", { name: /Review and submit/ }).query()).toBeNull();
    });

    it("puts a refused address on Step 1's email field", async () => {
        formState.requestEmail.mutate.mockImplementation(
            (_email: string, callbacks?: { onError?: (e: Error) => void }) =>
                callbacks?.onError?.(
                    new AccountRequestError(422, [
                        {
                            field: "email",
                            keyword: "IDA.EMAIL.INVALID_FORMAT",
                            message: "That address cannot be used.",
                        },
                    ]),
                ),
        );
        const { screen: pending, rerender } = renderForm();
        const screen = await pending;

        await fillAndContinue(screen);
        await rerender();

        await vi.waitFor(() =>
            expect(screen.getByText("That address cannot be used.").elements().length).toBeGreaterThan(0),
        );
        await expect
            .element(screen.getByRole("heading", { name: "Step 1: Provide your information" }))
            .toBeVisible();
    });

    it("reviews the answers as the citizen reads them", async () => {
        emailAnswers({ email: "jane@example.com", verified: true });
        const screen = await renderForm().screen;
        await fillAndContinue(screen);

        const table = screen.getByRole("table");
        await expect.element(table.getByRole("row", { name: /First name Jane/ })).toBeVisible();
        await expect.element(table.getByRole("row", { name: /Gender Woman\/Girl/ })).toBeVisible();
        await expect
            .element(table.getByRole("row", { name: /Social Insurance Number \(SIN\) 050-082-833/ }))
            .toBeVisible();
    });

    it("asks for the spec's consent on the review and sends it with the answers", async () => {
        formState.spec.data = {
            version: 4,
            spec: {
                ...specV4,
                components: [
                    ...specV4.components,
                    { type: "content", key: "consentHeading", input: false, html: "<h2>Registration consent</h2>" },
                    {
                        type: "checkbox",
                        key: "consent",
                        label: "I agree to the Terms of Use.",
                        input: true,
                        validate: { required: true },
                    },
                ],
            },
        };
        emailAnswers({ email: "jane@example.com", verified: true });
        const screen = await renderForm().screen;
        await fillAndContinue(screen);

        // Not given: nothing is sent.
        await screen.getByRole("button", { name: "Complete registration" }).click();
        await new Promise((resolve) => setTimeout(resolve, 300));
        expect(formState.submit.mutate).not.toHaveBeenCalled();

        await screen.getByRole("checkbox", { name: /I agree to the Terms of Use/ }).click({ force: true });
        await screen.getByRole("button", { name: "Complete registration" }).click();

        await vi.waitFor(() => expect(formState.submit.mutate).toHaveBeenCalledOnce());
        const [input] = formState.submit.mutate.mock.calls[0] as [{ answers: Record<string, unknown> }];
        expect(input.answers).toMatchObject({ sin: "050082833", consent: true, email: "jane@example.com" });
    });

    it("takes the citizen back to Step 2 when the API says the address is not verified", async () => {
        emailAnswers({ email: "jane@example.com", verified: true });
        refuseWith([
            {
                field: "email",
                keyword: "REGISTRATION.EMAIL.NOT_VERIFIED",
                message: "Verify your email address before you complete your registration.",
            },
        ]);
        const screen = await renderForm().screen;
        await fillAndContinue(screen);

        await screen.getByRole("button", { name: "Complete registration" }).click();

        await expect
            .element(screen.getByRole("heading", { name: "Step 2: Verify your email" }))
            .toBeVisible();
    });
});
