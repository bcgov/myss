import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import {
  MemoryRouter,
  Route,
  Routes,
  useParams,
  useSearchParams,
} from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";

import FormManagementPage from "./FormManagementPage";
import { listForms } from "@/api/forms";
import type { FormSummary } from "@/api/forms";

// The forms list against a stubbed admin API. listForms is mocked; the row
// rendering, version display and editor links are the real component.

vi.mock("@/api/forms", async (importActual) => ({
  ...(await importActual<typeof import("@/api/forms")>()),
  listForms: vi.fn(),
  getDraft: vi.fn(),
  saveDraft: vi.fn(),
  publishForm: vi.fn(),
}));

const mockListForms = vi.mocked(listForms);

const forms: FormSummary[] = [
  {
    formSpecId: "eligibility-estimator",
    title: "Eligibility Estimator",
    versions: [
      { version: 1, isPublished: true },
      { version: 2, isPublished: false },
    ],
  },
  {
    formSpecId: "bus-pass",
    title: null,
    versions: [{ version: 1, isPublished: true }],
  },
];

// Stands in for the editor so a test can see where the New form action went
// without pulling the whole editor in.
function EditorProbe() {
  const { formSpecId } = useParams();
  const [searchParams] = useSearchParams();
  return (
    <p>
      Editor opened: {formSpecId} / new=
      {searchParams.has("new") ? "yes" : "no"} / title=
      {searchParams.get("title") ?? "none"}
    </p>
  );
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={["/admin/form-management"]}>
        <Routes>
          <Route
            path="/admin/form-management"
            element={<FormManagementPage />}
          />
          <Route
            path="/admin/form-management/:formSpecId"
            element={<EditorProbe />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.clearAllMocks();
});

describe("FormManagementPage", () => {
  it("lists each form with its versions and links to the editor", async () => {
    mockListForms.mockResolvedValue(forms);

    const screen = await renderPage();

    // One link per form; a form without a title falls back to its id.
    const estimatorLink = screen.getByRole("link", {
      name: "Eligibility Estimator",
    });
    await expect.element(estimatorLink).toBeVisible();
    await expect
      .element(estimatorLink)
      .toHaveAttribute("href", "/admin/form-management/eligibility-estimator");

    const busPassLink = screen.getByRole("link", { name: "bus-pass" });
    await expect
      .element(busPassLink)
      .toHaveAttribute("href", "/admin/form-management/bus-pass");

    // Version numbers and the published/draft state render.
    await expect.element(screen.getByText("v2")).toBeVisible();
    await expect.element(screen.getByText("Draft")).toBeVisible();
    await expect.element(screen.getByText("Published").first()).toBeVisible();
  });

  it("falls back to the form id when the title is blank", async () => {
    mockListForms.mockResolvedValue([
      {
        formSpecId: "blank-title-form",
        title: "   ",
        versions: [{ version: 1, isPublished: true }],
      },
    ]);

    const screen = await renderPage();

    await expect
      .element(screen.getByRole("link", { name: "blank-title-form" }))
      .toBeVisible();
  });

  it("shows an empty state when there are no forms", async () => {
    mockListForms.mockResolvedValue([]);

    const screen = await renderPage();

    await expect.element(screen.getByText("No forms found.")).toBeVisible();
  });

  it("shows an error when the list cannot be loaded", async () => {
    mockListForms.mockRejectedValue(new Error("Forms list failed (500)"));

    const screen = await renderPage();

    const alert = screen.getByRole("alert");
    await expect.element(alert).toBeVisible();
    await expect.element(alert).toHaveTextContent("Could not load forms");
  });

  it("refuses an invalid form ID with an inline message", async () => {
    mockListForms.mockResolvedValue(forms);

    const screen = await renderPage();

    await screen.getByRole("button", { name: "New form" }).click();
    await screen.getByRole("textbox", { name: "Form ID" }).fill("My Form!");
    await screen.getByRole("button", { name: "Create" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent(
        "Enter a form ID using only lowercase letters, numbers and hyphens.",
      );
    // Still on the list; nothing navigated.
    await expect
      .element(screen.getByText(/Editor opened/))
      .not.toBeInTheDocument();
  });

  it("refuses an ID that already exists", async () => {
    mockListForms.mockResolvedValue(forms);

    const screen = await renderPage();

    await screen.getByRole("button", { name: "New form" }).click();
    await screen.getByRole("textbox", { name: "Form ID" }).fill("bus-pass");
    await screen.getByRole("button", { name: "Create" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent('A form with the ID "bus-pass" already exists.');
    await expect
      .element(screen.getByText(/Editor opened/))
      .not.toBeInTheDocument();
  });

  it("opens the editor as a new form for a valid ID and title", async () => {
    mockListForms.mockResolvedValue(forms);

    const screen = await renderPage();

    await screen.getByRole("button", { name: "New form" }).click();
    await screen.getByRole("textbox", { name: "Form ID" }).fill("test-intake");
    await screen.getByRole("textbox", { name: "Title" }).fill("Test Intake");
    await screen.getByRole("button", { name: "Create" }).click();

    await expect
      .element(
        screen.getByText(
          "Editor opened: test-intake / new=yes / title=Test Intake",
        ),
      )
      .toBeVisible();
  });
});
