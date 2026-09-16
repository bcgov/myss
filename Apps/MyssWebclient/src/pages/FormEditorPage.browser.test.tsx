import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";

import FormEditorPage from "./FormEditorPage";
import {
  FormLoadError,
  SpecRejectedError,
  getDraft,
  publishForm,
  saveDraft,
} from "@/api/forms";
import type { FormSpecPayload } from "@/api/forms";
import { getEstimatorRates } from "@/api/eligibility";
import { registerBcgovComponents } from "@/formio/bcgovComponents";

// The editor against a stubbed admin API. Only the calls are mocked — the real
// error classes are kept (the page does `instanceof FormLoadError`) — and the
// Form.io render (incl. the custom bcgovAccordion) and the edit panel are the
// real code, so the accordion proves registration and the panel proves editing.

vi.mock("@/api/forms", async (importActual) => ({
  ...(await importActual<typeof import("@/api/forms")>()),
  getDraft: vi.fn(),
  listForms: vi.fn(),
  saveDraft: vi.fn(),
  publishForm: vi.fn(),
}));

// The estimator-only rates panel reads this; keep the rest of the module real.
vi.mock("@/api/eligibility", async (importActual) => ({
  ...(await importActual<typeof import("@/api/eligibility")>()),
  getEstimatorRates: vi.fn(),
}));

const mockGetDraft = vi.mocked(getDraft);
const mockSaveDraft = vi.mocked(saveDraft);
const mockPublishForm = vi.mocked(publishForm);
const mockGetRates = vi.mocked(getEstimatorRates);

// The page registers this too; calling here mirrors the estimator test and
// keeps the accordion from rendering as a blank slot in isolation.
registerBcgovComponents();

const draft: FormSpecPayload = {
  formSpecId: "test-form",
  version: 2,
  title: "Test Form",
  spec: {
    display: "form",
    components: [
      {
        type: "textfield",
        key: "fullName",
        label: "Full name",
        input: true,
      },
      {
        // Hidden on the live form until fullName === "reveal"; the preview
        // strips this conditional so it must still render.
        type: "textfield",
        key: "conditionalField",
        label: "Conditionally shown field",
        input: true,
        conditional: { json: { "===": [{ var: "data.fullName" }, "reveal"] } },
      },
      {
        type: "bcgovAccordion",
        key: "help",
        accordionLabel: "What does status mean?",
        accordionBody: "<p>It means your immigration status.</p>",
        input: false,
      },
    ],
  } as FormSpecPayload["spec"],
};

function renderPage(formSpecId = "test-form") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter
        initialEntries={[`/admin/form-management/${formSpecId}`]}
      >
        <Routes>
          <Route
            path="/admin/form-management/:formSpecId"
            element={<FormEditorPage />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.clearAllMocks();
});

describe("FormEditorPage", () => {
  it("renders the spec fields and the bcgovAccordion", async () => {
    mockGetDraft.mockResolvedValue(draft);

    const screen = await renderPage();

    await expect.element(screen.getByText("Full name")).toBeVisible();
    await expect
      .element(screen.getByText("What does status mean?"))
      .toBeVisible();
    // A field the live form would hide behind a conditional still shows here.
    await expect
      .element(screen.getByText("Conditionally shown field"))
      .toBeVisible();
    // The rates panel is estimator-only, so it's absent for this form.
    await expect
      .element(screen.getByText("Eligibility rates"))
      .not.toBeInTheDocument();
  });

  it("shows the eligibility rates panel for the estimator form", async () => {
    mockGetDraft.mockResolvedValue({
      ...draft,
      formSpecId: "eligibility-estimator",
    });
    mockGetRates.mockResolvedValue({
      effectiveDate: "2023-08-01",
      incomeRows: [
        { familySize: 1, a: 1060, b: 1260, c: 1360, d: 1460, e: 1560 },
      ],
      assetLimits: { a: 5000, b: 10000, c: 100000, d: 200000 },
    });

    const screen = await renderPage("eligibility-estimator");

    await expect.element(screen.getByText("Eligibility rates")).toBeVisible();
    await expect.element(screen.getByText("$1,060")).toBeVisible();
  });

  it("shows a not-found message for an unknown form (404)", async () => {
    mockGetDraft.mockRejectedValue(
      new FormLoadError(404, "Draft fetch failed (404)"),
    );

    const screen = await renderPage();

    const alert = screen.getByRole("alert");
    await expect.element(alert).toBeVisible();
    await expect.element(alert).toHaveTextContent("could not be found");
  });

  it("shows a generic error for other load failures", async () => {
    mockGetDraft.mockRejectedValue(
      new FormLoadError(500, "Draft fetch failed (500)"),
    );

    const screen = await renderPage();

    const alert = screen.getByRole("alert");
    await expect.element(alert).toBeVisible();
    await expect.element(alert).toHaveTextContent("Could not load this form");
  });

  it("shows each field's key read-only and editing a label updates the preview", async () => {
    mockGetDraft.mockResolvedValue(draft);

    const screen = await renderPage();

    // The key is shown as plain text, not an editable field.
    await expect.element(screen.getByText("fullName")).toBeVisible();

    await screen
      .getByRole("textbox", { name: "Label for fullName" })
      .fill("Your legal name");

    // The preview re-renders with the new label.
    await expect.element(screen.getByText("Your legal name")).toBeVisible();
  });

  it("saves the edited working copy as a draft and confirms", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();

    await screen
      .getByRole("textbox", { name: "Label for fullName" })
      .fill("Your legal name");
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect.element(screen.getByText("Draft saved.")).toBeVisible();

    expect(mockSaveDraft).toHaveBeenCalledTimes(1);
    const [id, input] = mockSaveDraft.mock.calls[0];
    expect(id).toBe("test-form");
    expect(input.title).toBe("Test Form");
    const saved = input.spec.components?.find(
      (c) => (c as { key?: string }).key === "fullName",
    ) as { label?: string } | undefined;
    expect(saved?.label).toBe("Your legal name");
  });

  it("clears the saved confirmation once the draft is edited again", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();

    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText("Draft saved.")).toBeVisible();

    // Editing again means there are unsaved changes, so the confirmation goes.
    await screen
      .getByRole("textbox", { name: "Label for fullName" })
      .fill("Changed again");
    await expect
      .element(screen.getByText("Draft saved."))
      .not.toBeInTheDocument();
  });

  it("publishes the working copy and reports the new version", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 4 });
    mockPublishForm.mockResolvedValue({ version: 4 });

    const screen = await renderPage();

    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(screen.getByText("Published version 4."))
      .toBeVisible();
    expect(mockSaveDraft).toHaveBeenCalledTimes(1);
    expect(mockPublishForm).toHaveBeenCalledTimes(1);
  });

  it("surfaces a 422 as an error summary and keeps the edits on screen", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockRejectedValue(
      new SpecRejectedError(422, [
        { field: "fullName", keyword: "required", message: "Label is required." },
      ]),
    );

    const screen = await renderPage();

    await screen
      .getByRole("textbox", { name: "Label for fullName" })
      .fill("Edited label");
    await screen.getByRole("button", { name: "Publish" }).click();

    // The summary lists the server's reason...
    await expect.element(screen.getByText("There is a problem")).toBeVisible();
    await expect.element(screen.getByText("Label is required.")).toBeVisible();
    // ...and the edit is still on screen (working copy preserved).
    await expect.element(screen.getByText("Edited label")).toBeVisible();
    expect(mockPublishForm).not.toHaveBeenCalled();
  });

  it("surfaces a publish failure that carries no error list (e.g. 502)", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 4 });
    // A 502 from the content engine throws a SpecRejectedError with no errors —
    // this must not fail silently.
    mockPublishForm.mockRejectedValue(new SpecRejectedError(502, []));

    const screen = await renderPage();

    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(screen.getByText(/Could not publish this form \(error 502\)/))
      .toBeVisible();
  });
});
