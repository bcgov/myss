import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import { paths } from "@/routes/paths";

const formState = vi.hoisted(() => ({
    spec: {
        data: undefined as { title?: string; version: number; spec: object } | undefined,
        error: null as Error | null,
        isPending: false,
    },
    submit: {
        data: undefined as object | undefined,
        error: null as Error | null,
        mutate: vi.fn(),
    },
}));

vi.mock("@/hooks/usePocForm", () => ({
    useFormSpec: () => formState.spec,
    useSubmitForm: () => formState.submit,
}));

import RegistrationForm from "./RegistrationForm";

function renderForm() {
    return render(
        <MemoryRouter initialEntries={[paths.register]}>
            <Routes>
                <Route path={paths.register} element={<RegistrationForm />} />
                <Route path={paths.dashboard} element={<p>Dashboard landing</p>} />
            </Routes>
        </MemoryRouter>,
    );
}

describe("RegistrationForm", () => {
    beforeEach(() => {
        formState.spec.data = undefined;
        formState.spec.error = null;
        formState.spec.isPending = false;
        formState.submit.data = undefined;
        formState.submit.error = null;
        formState.submit.mutate.mockClear();
    });

    it("shows loading while the registration spec is fetched", async () => {
        formState.spec.isPending = true;
        const screen = await renderForm();

        await expect.element(screen.getByText("Loading form…")).toBeInTheDocument();
    });

    it("shows the spec failure", async () => {
        formState.spec.error = new Error("Spec fetch failed (404)");
        const screen = await renderForm();

        await expect
            .element(screen.getByText("Could not load the form: Spec fetch failed (404)"))
            .toBeInTheDocument();
    });

    it("renders the registration form title", async () => {
        formState.spec.data = {
            title: "Registration information",
            version: 3,
            spec: { display: "form", components: [] },
        };
        const screen = await renderForm();

        await expect
            .element(screen.getByRole("heading", { name: "Registration information" }))
            .toBeInTheDocument();
    });

    it("redirects to the dashboard after a successful submission", async () => {
        formState.submit.data = {};
        const screen = await renderForm();

        await expect.element(screen.getByText("Dashboard landing")).toBeInTheDocument();
    });
});
