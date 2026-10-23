import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { userEvent } from "vitest/browser";
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
import { getFormVersion, listForms } from "@/api/forms";
import type { FormSpecPayload, FormSummary } from "@/api/forms";

// The forms list against a stubbed admin API. The calls are mocked; the row
// rendering, version labels, JSON dialog and editor links are the real code.

vi.mock("@/api/forms", async (importActual) => ({
  ...(await importActual<typeof import("@/api/forms")>()),
  listForms: vi.fn(),
  getDraft: vi.fn(),
  getFormVersion: vi.fn(),
  saveDraft: vi.fn(),
  publishForm: vi.fn(),
}));

const mockListForms = vi.mocked(listForms);
const mockGetFormVersion = vi.mocked(getFormVersion);

function versionPayload(formSpecId: string, version: number): FormSpecPayload {
  return {
    formSpecId,
    version,
    title: null,
    spec: {
      display: "form",
      components: [{ type: "textfield", key: `field${version}`, input: true }],
    } as FormSpecPayload["spec"],
  };
}

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

// Stands in for the JSON page, showing the address Create form from JSON
// opened.
function JsonPageProbe() {
  const { formSpecId } = useParams();
  const [searchParams] = useSearchParams();
  return (
    <p>
      JSON page opened: {formSpecId} / title=
      {searchParams.get("title") ?? "none"} / confirm=
      {searchParams.get("confirm") ?? "none"}
    </p>
  );
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={["/form-management"]}>
        <Routes>
          <Route
            path="/form-management"
            element={<FormManagementPage />}
          />
          <Route
            path="/form-management/:formSpecId"
            element={<EditorProbe />}
          />
          <Route
            path="/form-management/:formSpecId/from-json"
            element={<JsonPageProbe />}
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
      .toHaveAttribute("href", "/form-management/eligibility-estimator");

    const busPassLink = screen.getByRole("link", { name: "bus-pass" });
    await expect
      .element(busPassLink)
      .toHaveAttribute("href", "/form-management/bus-pass");

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

describe("FormManagementPage: version labels", () => {
  it("opens a published version's JSON from its label", async () => {
    mockListForms.mockResolvedValue(forms);
    mockGetFormVersion.mockImplementation(async (formSpecId, version) =>
      versionPayload(formSpecId, version),
    );
    const screen = await renderPage();

    await screen
      .getByRole("button", {
        name: "v1 Published, view JSON of Eligibility Estimator",
      })
      .click();

    await expect
      .element(
        screen.getByRole("dialog", {
          name: "Eligibility Estimator · version 1 · Published",
        }),
      )
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: "Form JSON" }))
      .toHaveValue(
        JSON.stringify(
          versionPayload("eligibility-estimator", 1).spec,
          null,
          2,
        ),
      );
    expect(mockGetFormVersion).toHaveBeenCalledWith("eligibility-estimator", 1);
  });

  it("opens a draft version's JSON from its label", async () => {
    mockListForms.mockResolvedValue(forms);
    mockGetFormVersion.mockImplementation(async (formSpecId, version) =>
      versionPayload(formSpecId, version),
    );
    const screen = await renderPage();

    await screen
      .getByRole("button", {
        name: "v2 Draft, view JSON of Eligibility Estimator",
      })
      .click();

    await expect
      .element(
        screen.getByRole("dialog", {
          name: "Eligibility Estimator · version 2 · Draft",
        }),
      )
      .toBeVisible();
    expect(mockGetFormVersion).toHaveBeenCalledWith("eligibility-estimator", 2);
  });

  it("names each label by its visible text first", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();

    const label = screen.getByRole("button", { name: /^v2 Draft/ });
    await expect
      .element(label)
      .toHaveAccessibleName("v2 Draft, view JSON of Eligibility Estimator");
    await expect.element(label).toHaveTextContent("v2Draft");
  });

  it("returns focus to the label when the dialog closes", async () => {
    mockListForms.mockResolvedValue(forms);
    mockGetFormVersion.mockImplementation(async (formSpecId, version) =>
      versionPayload(formSpecId, version),
    );
    const screen = await renderPage();
    const label = screen.getByRole("button", {
      name: "v2 Draft, view JSON of Eligibility Estimator",
    });

    await label.click();
    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect.element(screen.getByRole("dialog")).not.toBeInTheDocument();
    await expect.element(label).toHaveFocus();
  });
});

describe("FormManagementPage: Create form from JSON", () => {
  type Screen = Awaited<ReturnType<typeof renderPage>>;

  const late: FormSummary = {
    formSpecId: "zz-late",
    title: "Late Form",
    versions: [{ version: 1, isPublished: true }],
  };

  function deferred<T>() {
    let resolve!: (value: T) => void;
    const promise = new Promise<T>((settle) => {
      resolve = settle;
    });
    return { promise, resolve };
  }

  async function openPanel(screen: Screen) {
    await screen.getByRole("button", { name: "Create form from JSON" }).click();
    await expect
      .element(screen.getByRole("heading", { name: "Create form from JSON" }))
      .toBeVisible();
  }

  // The design system hides the real radio input, so it is clicked directly.
  function chooseKind(screen: Screen, name: string) {
    (screen.getByRole("radio", { name }).element() as HTMLElement).click();
  }

  // The Select's button is named by its value, then its label.
  function formSelect(screen: Screen) {
    return screen
      .getByRole("form", { name: "Create form from JSON" })
      .getByRole("button", { name: /Form$/ });
  }

  async function chooseForm(screen: Screen, name: string) {
    await formSelect(screen).click();
    await screen.getByRole("option", { name }).click();
  }

  function titleField(screen: Screen) {
    return screen.getByRole("textbox", { name: "Title" });
  }

  function formIdField(screen: Screen) {
    return screen.getByRole("textbox", { name: "Form ID" });
  }

  it("asks what is being created, and lists the forms by title and ID", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();

    await openPanel(screen);

    await expect
      .element(
        screen.getByRole("radiogroup", { name: "What are you creating?" }),
      )
      .toBeVisible();
    const existing = screen.getByRole("radio", {
      name: "A new version of an existing form",
    });
    await expect.element(existing).toBeChecked();
    await expect.element(existing).toHaveFocus();
    expect(formIdField(screen).query()).toBeNull();

    await formSelect(screen).click();
    const estimator = screen.getByRole("option", {
      name: "Eligibility Estimator",
    });
    await expect.element(estimator).toBeVisible();
    await expect
      .element(estimator)
      .toHaveAccessibleDescription("eligibility-estimator");
    expect(screen.getByRole("option").elements()).toHaveLength(2);
  });

  it("shows a chosen form's title, read-only", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);

    await chooseForm(screen, "Eligibility Estimator");

    await expect
      .element(formSelect(screen))
      .toHaveAccessibleName(/^Eligibility Estimator/);
    await expect
      .element(titleField(screen))
      .toHaveValue("Eligibility Estimator");
    await expect.element(titleField(screen)).toHaveAttribute("readonly");
    await expect
      .element(titleField(screen))
      .toHaveAccessibleDescription("A new version keeps this form's title.");

    // A form without a title leaves the field empty rather than showing its ID.
    await chooseForm(screen, "bus-pass");
    await expect.element(titleField(screen)).toHaveValue("");
  });

  it("asks before adding a version to a published form, and records the answer", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");

    await screen.getByRole("button", { name: "Continue" }).click();

    const warning = screen.getByRole("alertdialog", {
      name: 'Create a new version of "bus-pass"?',
    });
    await expect.element(warning).toBeVisible();
    await expect
      .element(warning)
      .toHaveAccessibleDescription(
        /Your JSON will be saved as version 2, as a draft\. Version 1 stays live/,
      );

    await screen.getByRole("button", { name: "Create version 2" }).click();

    await expect
      .element(
        screen.getByText(
          "JSON page opened: bus-pass / title=none / confirm=new-2",
        ),
      )
      .toBeVisible();
  });

  it("says an unpublished draft will be replaced", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "Eligibility Estimator");

    await screen.getByRole("button", { name: "Continue" }).click();

    await expect
      .element(
        screen.getByRole("alertdialog", {
          name: 'Replace the unpublished draft of "Eligibility Estimator"?',
        }),
      )
      .toBeVisible();
    await screen
      .getByRole("button", { name: "Replace draft version 2" })
      .click();
    await expect
      .element(
        screen.getByText(
          "JSON page opened: eligibility-estimator / title=none / confirm=replace-2",
        ),
      )
      .toBeVisible();
  });

  it("asks for a form when none is chosen, without a read", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);

    await screen.getByRole("button", { name: "Continue" }).click();

    await expect.element(formSelect(screen)).toHaveFocus();
    await expect
      .element(formSelect(screen))
      .toHaveAccessibleDescription(/Choose a form\./);
    expect(mockListForms).toHaveBeenCalledTimes(1);
  });

  it("refuses a chosen form that the new read no longer lists", async () => {
    mockListForms.mockResolvedValueOnce(forms).mockResolvedValue([forms[0]]);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");

    await screen.getByRole("button", { name: "Continue" }).click();

    await expect.element(formSelect(screen)).toHaveFocus();
    await expect
      .element(formSelect(screen))
      .toHaveAccessibleDescription(/no longer in the forms list/);
    expect(screen.getByText(/JSON page opened/).query()).toBeNull();
  });

  it("offers a new form first when there are no forms", async () => {
    mockListForms.mockResolvedValue([]);
    const screen = await renderPage();

    await openPanel(screen);

    const newForm = screen.getByRole("radio", { name: "A new form" });
    await expect.element(newForm).toBeChecked();
    await expect.element(newForm).toHaveFocus();
    await expect.element(formIdField(screen)).toBeVisible();
  });

  it("keeps each answer's values apart when switching between them", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");
    chooseKind(screen, "A new form");
    await formIdField(screen).fill("zz-json-new");

    chooseKind(screen, "A new version of an existing form");
    await screen.getByRole("button", { name: "Continue" }).click();
    await expect
      .element(
        screen.getByRole("alertdialog", {
          name: 'Create a new version of "bus-pass"?',
        }),
      )
      .toBeVisible();
    await screen.getByRole("button", { name: "Cancel" }).last().click();

    chooseKind(screen, "A new form");
    await expect.element(formIdField(screen)).toHaveValue("zz-json-new");
    await screen.getByRole("button", { name: "Continue" }).click();
    await expect
      .element(
        screen.getByText(
          "JSON page opened: zz-json-new / title=none / confirm=none",
        ),
      )
      .toBeVisible();
  });

  it("opens the JSON page for a new form, with the typed title", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);

    chooseKind(screen, "A new form");
    expect(formSelect(screen).query()).toBeNull();
    await formIdField(screen).fill("zz-json-new");
    await titleField(screen).fill("New JSON form");
    await screen.getByRole("button", { name: "Continue" }).click();

    await expect
      .element(
        screen.getByText(
          "JSON page opened: zz-json-new / title=New JSON form / confirm=none",
        ),
      )
      .toBeVisible();
    expect(mockListForms).toHaveBeenCalledTimes(2);
  });

  it("warns about a typed ID that is already a form, and returns focus to Continue", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);
    chooseKind(screen, "A new form");
    await formIdField(screen).fill("bus-pass");
    const submit = screen.getByRole("button", { name: "Continue" });

    // Enter in the field submits, so Continue did not have focus.
    await userEvent.keyboard("{Enter}");
    await expect
      .element(
        screen.getByRole("alertdialog", {
          name: 'Create a new version of "bus-pass"?',
        }),
      )
      .toBeVisible();
    await screen.getByRole("button", { name: "Cancel" }).last().click();

    await expect
      .element(screen.getByRole("alertdialog"))
      .not.toBeInTheDocument();
    await expect.element(submit).toHaveFocus();
    expect(screen.getByText(/JSON page opened/).query()).toBeNull();

    await submit.click();
    await expect.element(screen.getByRole("alertdialog")).toBeVisible();
    await userEvent.keyboard("{Escape}");
    await expect
      .element(screen.getByRole("alertdialog"))
      .not.toBeInTheDocument();
    await expect.element(submit).toHaveFocus();

    // Confirmed, it opens the JSON page without the typed title.
    await titleField(screen).fill("Ignored");
    await submit.click();
    await screen.getByRole("button", { name: "Create version 2" }).click();
    await expect
      .element(
        screen.getByText(
          "JSON page opened: bus-pass / title=none / confirm=new-2",
        ),
      )
      .toBeVisible();
  });

  it("asks about an ID that exists only in the read made on Continue", async () => {
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockResolvedValue([...forms, late]);
    const screen = await renderPage();
    await openPanel(screen);
    chooseKind(screen, "A new form");

    await formIdField(screen).fill("zz-late");
    await screen.getByRole("button", { name: "Continue" }).click();

    await expect
      .element(
        screen.getByRole("alertdialog", {
          name: 'Create a new version of "Late Form"?',
        }),
      )
      .toBeVisible();
    expect(screen.getByText(/JSON page opened/).query()).toBeNull();
  });

  it("refuses a badly formed ID, with focus on the field", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);
    chooseKind(screen, "A new form");

    await formIdField(screen).fill("Not Valid");
    await screen.getByRole("button", { name: "Continue" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent(/Enter a form ID using only lowercase/);
    await expect.element(formIdField(screen)).toHaveFocus();
    await expect
      .element(formIdField(screen))
      .toHaveAttribute("aria-invalid", "true");
    await expect
      .element(formIdField(screen))
      .toHaveAccessibleDescription(
        /Lowercase letters, numbers and hyphens\..*Enter a form ID/,
      );
    expect(mockListForms).toHaveBeenCalledTimes(1);
  });

  it("keeps the list and the panel when the read fails", async () => {
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockRejectedValueOnce(new Error("Forms list failed (500)"));
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");

    await screen.getByRole("button", { name: "Continue" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Couldn't check the form. Try again.");
    await expect
      .element(screen.getByRole("link", { name: "Eligibility Estimator" }))
      .toBeVisible();
    await expect.element(formSelect(screen)).toHaveAccessibleName(/^bus-pass/);
  });

  it("reads once while Checking…, however often Continue is selected", async () => {
    const read = deferred<FormSummary[]>();
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockReturnValueOnce(read.promise);
    const screen = await renderPage();
    await openPanel(screen);
    chooseKind(screen, "A new form");
    await formIdField(screen).fill("zz-json-new");

    await screen.getByRole("button", { name: "Continue" }).click();
    const checking = screen.getByRole("button", { name: "Checking…" });
    await expect.element(checking).toBeVisible();
    await checking.click();
    expect(mockListForms).toHaveBeenCalledTimes(2);

    read.resolve(forms);
    await expect
      .element(screen.getByText(/JSON page opened: zz-json-new/))
      .toBeVisible();
  });

  it("ignores a read that ends after the panel is cancelled", async () => {
    const read = deferred<FormSummary[]>();
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockReturnValueOnce(read.promise);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");

    await screen.getByRole("button", { name: "Continue" }).click();
    await screen.getByRole("button", { name: "Cancel" }).click();
    read.resolve(forms);
    await read.promise;
    await new Promise((settle) => setTimeout(settle, 50));

    expect(screen.getByRole("alertdialog").query()).toBeNull();
    await expect
      .element(screen.getByRole("button", { name: "Create form from JSON" }))
      .toHaveFocus();
  });

  it("ignores a read once another form is chosen", async () => {
    const read = deferred<FormSummary[]>();
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockReturnValueOnce(read.promise);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");

    await screen.getByRole("button", { name: "Continue" }).click();
    await chooseForm(screen, "Eligibility Estimator");
    read.resolve(forms);
    await read.promise;
    await new Promise((settle) => setTimeout(settle, 50));

    expect(screen.getByRole("alertdialog").query()).toBeNull();
    await expect
      .element(screen.getByRole("button", { name: "Continue" }))
      .toBeVisible();
  });

  it("ignores a read once the ID is edited", async () => {
    const read = deferred<FormSummary[]>();
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockReturnValueOnce(read.promise);
    const screen = await renderPage();
    await openPanel(screen);
    chooseKind(screen, "A new form");
    await formIdField(screen).fill("zz-json-new");

    await screen.getByRole("button", { name: "Continue" }).click();
    await formIdField(screen).fill("zz-json-other");
    read.resolve(forms);
    await read.promise;
    await new Promise((settle) => setTimeout(settle, 50));

    expect(screen.getByText(/JSON page opened/).query()).toBeNull();
    await expect.element(formIdField(screen)).toHaveValue("zz-json-other");
  });

  it("ignores a read once the answer has changed", async () => {
    const read = deferred<FormSummary[]>();
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockReturnValueOnce(read.promise);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");

    await screen.getByRole("button", { name: "Continue" }).click();
    chooseKind(screen, "A new form");
    read.resolve(forms);

    await expect
      .element(screen.getByRole("button", { name: "Continue" }))
      .toBeVisible();
    expect(screen.getByRole("alertdialog").query()).toBeNull();
    expect(screen.getByText(/JSON page opened/).query()).toBeNull();
  });

  it("keeps focus inside the warning", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);
    await chooseForm(screen, "bus-pass");
    await screen.getByRole("button", { name: "Continue" }).click();

    const warning = screen.getByRole("alertdialog");
    await expect.element(warning).toBeVisible();
    await vi.waitFor(() =>
      expect(warning.element().contains(document.activeElement)).toBe(true),
    );
    for (let press = 0; press < 4; press += 1) {
      await userEvent.keyboard("{Tab}");
      expect(warning.element().contains(document.activeElement)).toBe(true);
    }
  });

  it("returns focus to its button when the panel is cancelled", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();
    await openPanel(screen);

    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect
      .element(screen.getByRole("button", { name: "Create form from JSON" }))
      .toHaveFocus();
  });

  it("returns focus to New form when its panel is cancelled", async () => {
    mockListForms.mockResolvedValue(forms);
    const screen = await renderPage();

    await screen.getByRole("button", { name: "New form" }).click();
    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect
      .element(screen.getByRole("button", { name: "New form" }))
      .toHaveFocus();
  });

  it("keeps a text field on New form and refuses an ID taken since the page loaded", async () => {
    mockListForms
      .mockResolvedValueOnce(forms)
      .mockResolvedValue([...forms, late]);
    const screen = await renderPage();

    await screen.getByRole("button", { name: "New form" }).click();
    expect(screen.getByRole("radio").query()).toBeNull();
    await formIdField(screen).fill("zz-late");
    await screen.getByRole("button", { name: "Create" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent(
        'A form with the ID "zz-late" already exists. Choose another.',
      );
    expect(screen.getByText(/Editor opened/).query()).toBeNull();
  });
});
