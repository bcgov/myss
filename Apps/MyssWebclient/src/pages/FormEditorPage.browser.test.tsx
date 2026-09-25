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
import { registerBcgovComponents } from "@/widgets/formio/bcgovComponents";
import {
  ALLOWED_COMPONENT_TYPES,
  builderOptions,
} from "@/widgets/formio/builderOptions";
import { Components } from "@formio/js";

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
      {
        type: "button",
        key: "submit",
        action: "submit",
        label: "Submit",
        input: true,
      },
    ],
  } as FormSpecPayload["spec"],
};

function renderPage(formSpecId = "test-form", search = "") {
  const queryClient = new QueryClient({
    // useDraft sets its own retry, which overrides `retry: false` here; zero
    // delay keeps its retries from adding seconds of backoff to the suite.
    defaultOptions: { queries: { retry: false, retryDelay: 0 } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter
        initialEntries={[`/admin/form-management/${formSpecId}${search}`]}
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

  it("creates a new form: template in the builder, saved as the first draft", async () => {
    const saved: FormSpecPayload = {
      formSpecId: "test-intake",
      version: 1,
      title: "Test Intake",
      spec: { display: "form", components: [] } as FormSpecPayload["spec"],
    };
    // A 404 with the new-form flag means "not stored yet"; after the save the
    // refetch finds the stored draft.
    mockGetDraft
      .mockRejectedValueOnce(new FormLoadError(404, "Draft fetch failed (404)"))
      .mockResolvedValue(saved);
    mockSaveDraft.mockResolvedValue(saved);

    const screen = await renderPage(
      "test-intake",
      "?new=1&title=Test+Intake",
    );

    // The title from the New form action heads the page; no not-found alert.
    await expect
      .element(screen.getByRole("heading", { name: "Test Intake" }))
      .toBeVisible();
    await expect
      .element(screen.getByText("could not be found"))
      .not.toBeInTheDocument();

    // A new form opens in the builder, on the template's submit button.
    await expect
      .element(screen.getByRole("button", { name: "Build form" }))
      .toHaveAttribute("aria-pressed", "true");
    await vi.waitFor(() => {
      expect(
        document.querySelector('[class~="formio-component-submit"]'),
      ).not.toBeNull();
    });

    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText("Draft saved.")).toBeVisible();

    const [id, input] = mockSaveDraft.mock.calls[0];
    expect(id).toBe("test-intake");
    expect(input.title).toBe("Test Intake");
    const components = (input.spec.components ?? []) as Record<
      string,
      unknown
    >[];
    expect(components.map((c) => c.key)).toEqual(["submit"]);
    expect(components[0].type).toBe("button");
  });

  it("treats the new flag on an existing form as normal editing", async () => {
    // A stale list can let a duplicate ID through the New form action; the
    // editor must then edit the stored form, and the URL's title must not
    // rename it.
    mockGetDraft.mockResolvedValue({ ...draft, title: null });
    mockSaveDraft.mockResolvedValue({ ...draft, title: null, version: 3 });

    const screen = await renderPage("test-form", "?new=1&title=Sneaky");

    await expect
      .element(screen.getByRole("heading", { name: "test-form", level: 1 }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Edit labels" }))
      .toHaveAttribute("aria-pressed", "true");

    await screen
      .getByRole("textbox", { name: "Label for fullName" })
      .fill("Edited label");
    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText("Draft saved.")).toBeVisible();

    const [, input] = mockSaveDraft.mock.calls[0];
    expect(input.title).toBeNull();
  });

  it("warns when a new form's first save lands on an ID that already existed", async () => {
    const saved: FormSpecPayload = {
      formSpecId: "test-intake",
      version: 4,
      title: "Test Intake",
      spec: { display: "form", components: [] } as FormSpecPayload["spec"],
    };
    mockGetDraft
      .mockRejectedValueOnce(new FormLoadError(404, "Draft fetch failed (404)"))
      .mockResolvedValue(saved);
    mockSaveDraft.mockResolvedValue(saved);

    const screen = await renderPage(
      "test-intake",
      "?new=1&title=Test+Intake",
    );

    await vi.waitFor(() => {
      expect(
        document.querySelector('[class~="formio-component-submit"]'),
      ).not.toBeNull();
    });
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(screen.getByText(/already existed/))
      .toBeVisible();
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

// The builder. Its palette and its settings dialog are driven by
// builderOptions, and both depend on the Form.io runtime — which types are
// registered, and what a component class builds its edit form from — so these
// checks live here rather than in builderOptions.unit.test.ts.

interface EditFormNode {
  key?: string;
  disabled?: boolean;
  components?: EditFormNode[];
  columns?: { components?: EditFormNode[] }[];
}

/** Every node under one tab of an edit form, flattened. */
function flatten(components: EditFormNode[] = [], out: EditFormNode[] = []) {
  for (const node of components) {
    out.push(node);
    flatten(node.components, out);
    for (const column of node.columns ?? []) flatten(column.components, out);
  }
  return out;
}

// The settings dialog is a tabbed form. Scope assertions to one tab: a
// radio's Data tab holds a URL-headers table with its own column keyed "key",
// which a whole-tree search would mistake for the component's API key.
function tabNodes(form: { components: EditFormNode[] }, tab: string) {
  const tabs = form.components.find((node) => node.key === "tabs");
  return flatten(
    tabs?.components?.find((node) => node.key === tab)?.components,
  );
}

function tabFields(form: { components: EditFormNode[] }, tab: string) {
  return tabNodes(form, tab)
    .map((node) => node.key)
    .filter((key): key is string => Boolean(key));
}

interface RegisteredComponent {
  builderInfo?: { schema?: { type?: string } };
  editForm?: (overrides: unknown) => { components: EditFormNode[] };
}

function registered(type: string): RegisteredComponent | undefined {
  return (
    Components as unknown as { components: Record<string, RegisteredComponent> }
  ).components[type];
}

describe("FormEditorPage build mode", () => {
  it("has a registered component behind every palette entry", () => {
    // A type the builder cannot resolve is dropped from the palette with no
    // warning, so the palette would quietly shrink instead of failing.
    for (const type of ALLOWED_COMPONENT_TYPES) {
      const component = registered(type);
      expect(component, `${type} is not registered with Form.io`).toBeDefined();
      expect(
        component?.builderInfo?.schema,
        `${type} exposes no builderInfo.schema`,
      ).toBeDefined();
    }
  });

  it("locks the component key for every type without removing it", () => {
    // Removing it would break the builder's label-to-key derivation, so the
    // field stays and is disabled instead.
    for (const type of ALLOWED_COMPONENT_TYPES) {
      const component = registered(type);
      const built = component!.editForm!(
        structuredClone(builderOptions.editForm[type]),
      );
      const keyField = tabNodes(built, "api").find(
        (node) => node.key === "key",
      );

      expect(keyField, `${type} has no key field`).toBeDefined();
      expect(keyField?.disabled, `${type} key field is editable`).toBe(true);
    }
  });

  it("removes multiple values from the question types' dialogs", () => {
    for (const type of ["textfield", "number", "select", "email"]) {
      const component = registered(type);
      const withOverride = tabFields(
        component!.editForm!(structuredClone(builderOptions.editForm[type])),
        "data",
      );
      const withoutOverride = tabFields(component!.editForm!(undefined), "data");

      expect(withoutOverride, `${type} has no multiple field`).toContain(
        "multiple",
      );
      expect(withOverride, `${type} still offers multiple`).not.toContain(
        "multiple",
      );
    }
  });

  it("does not add a Data tab to the types that have none", () => {
    for (const type of ["button", "content", "panel"]) {
      const component = registered(type);
      const built = component!.editForm!(
        structuredClone(builderOptions.editForm[type]),
      );
      expect(tabFields(built, "data"), `${type} gained a Data tab`).toEqual([]);
    }
  });

  it("gives the accordion heading and body fields in place of a label", () => {
    const accordion = registered("bcgovAccordion");
    const fields = tabFields(
      accordion!.editForm!(
        structuredClone(builderOptions.editForm.bcgovAccordion),
      ),
      "display",
    );

    expect(fields).toContain("accordionLabel");
    expect(fields).toContain("accordionBody");
    expect(fields).not.toContain("label");
  });

  it("offers only the allowed types in the palette", async () => {
    mockGetDraft.mockResolvedValue(draft);

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();

    // Read the palette out of the DOM rather than by role: two of the three
    // groups render collapsed, so their entries are present but not visible.
    await vi.waitFor(() => {
      expect(document.querySelectorAll(".formcomponent").length).toBeGreaterThan(
        0,
      );
    });
    const offered = [...document.querySelectorAll(".formcomponent")].map(
      (element) => element.getAttribute("data-key"),
    );

    expect(offered.slice().sort()).toEqual(
      ALLOWED_COMPONENT_TYPES.slice().sort(),
    );
    for (const stray of ["survey", "signature", "file", "datagrid"]) {
      expect(offered).not.toContain(stray);
    }
  });

  it("makes the palette reachable by keyboard", async () => {
    // Without keyboardBuilder the entries render with tabindex -1 and a drag is
    // the only way to add a field. The entry comes from the group that opens
    // on load: a collapsed group is display: none and cannot take focus.
    mockGetDraft.mockResolvedValue(draft);

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();

    const entry = await vi.waitFor(() => {
      const found = document.querySelector<HTMLElement>(
        '.formcomponent[data-key="radio"]',
      );
      expect(found).not.toBeNull();
      return found!;
    });

    expect(entry.tabIndex).toBe(0);
    entry.focus();
    expect(document.activeElement).toBe(entry);
  });

  it("saves a builder edit without rewriting the rest of the spec", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();

    // Deleting is a real builder edit that opens no settings dialog.
    const remove = await vi.waitFor(() => {
      const found = document
        .querySelector('[class~="formio-component-fullName"]')
        ?.closest(".builder-component")
        ?.querySelector<HTMLElement>('[ref="removeComponent"]');
      expect(found, "no remove control on the component").toBeTruthy();
      return found!;
    });
    remove.click();

    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText("Draft saved.")).toBeVisible();

    const [, input] = mockSaveDraft.mock.calls[0];
    const components = (input.spec.components ?? []) as Record<
      string,
      unknown
    >[];
    const keys = components.map((c) => c.key);

    // The edit reached the payload...
    expect(keys).not.toContain("fullName");
    // ...the untouched components are still there...
    expect(keys).toContain("conditionalField");
    expect(keys).toContain("help");
    expect(keys).toContain("submit");
    // ...and the spec was not replaced by Form.io's fully-defaulted form, which
    // carries a freshly generated id on every component.
    expect(components.some((c) => "id" in c)).toBe(false);
    // Untouched components keep the settings that carry meaning downstream.
    const accordion = components.find((c) => c.key === "help");
    expect(accordion?.accordionBody).toBe(
      "<p>It means your immigration status.</p>",
    );
    expect(accordion?.accordionLabel).toBe("What does status mean?");
    const conditional = components.find((c) => c.key === "conditionalField");
    expect(conditional?.conditional).toEqual({
      json: { "===": [{ var: "data.fullName" }, "reveal"] },
    });
  });

  it("drops a property whose value equals Form.io's default", () => {
    // Characterises a known fidelity loss rather than endorsing it: the
    // builder's schema omits any property already at its default, so a button
    // loses action:"submit". Harmless at render — Form.io re-applies the
    // default — but it means a save rewrites part of an untouched component.
    // If this assertion starts failing, the loss has been fixed; delete it.
    const button = registered("button");
    const defaults = button?.builderInfo?.schema as
      | { action?: string }
      | undefined;

    expect(defaults?.action).toBe("submit");
  });

  it("renders the custom BC Gov components inside the builder", async () => {
    // Both mount a React root in attach(). The builder attaches and detaches
    // components as the canvas is rebuilt, which the read-only preview never
    // does, so exercise them on the builder's own mount path.
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: {
        display: "form",
        components: [
          {
            type: "bcgovRadio",
            key: "hasStatus",
            label: "Do you have status?",
            input: true,
            values: [
              { label: "Yes", value: "yes" },
              { label: "No", value: "no" },
            ],
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
    });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();

    await expect
      .element(screen.getByText("Do you have status?"))
      .toBeVisible();
    await expect
      .element(screen.getByText("What does status mean?"))
      .toBeVisible();
  });

  it("opens the builder on the edits already made in labels mode", async () => {
    mockGetDraft.mockResolvedValue(draft);

    const screen = await renderPage();
    await screen
      .getByRole("textbox", { name: "Label for fullName" })
      .fill("Your legal name");
    await screen.getByRole("button", { name: "Build form" }).click();

    // The labels grid is unmounted, so the builder is the only thing that can
    // be showing this label. Seeded from the spec as fetched it would read
    // "Full name", and the builder's first change would undo the edit.
    await expect.element(screen.getByText("Your legal name")).toBeVisible();
    await expect.element(screen.getByText("Full name")).not.toBeInTheDocument();
  });
});
