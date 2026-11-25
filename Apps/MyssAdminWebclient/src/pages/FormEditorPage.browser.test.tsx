import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { page } from "vitest/browser";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";

import FormEditorPage from "./FormEditorPage";
import {
  FormLoadError,
  SpecRejectedError,
  VERSION_TAKEN,
  getDraft,
  publishForm,
  saveDraft,
} from "@/api/forms";
import type { FormSpecPayload } from "@/api/forms";
import { getEstimatorRates } from "@/api/eligibilityRates";
import {
  ALLOWED_COMPONENT_TYPES,
  builderOptions,
  registerBcgovComponents,
} from "@myss/formio";
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
vi.mock("@/api/eligibilityRates", async (importActual) => ({
  ...(await importActual<typeof import("@/api/eligibilityRates")>()),
  getEstimatorRates: vi.fn(),
}));

const mockGetDraft = vi.mocked(getDraft);
const mockSaveDraft = vi.mocked(saveDraft);
const mockPublishForm = vi.mocked(publishForm);
const mockGetRates = vi.mocked(getEstimatorRates);

// The page registers this too; calling here mirrors the estimator test and
// keeps the accordion from rendering as a blank slot in isolation.
registerBcgovComponents();

const notFound = () => new FormLoadError(404, "Draft fetch failed (404)");

/** A form with one repeat section, whose properties are `properties`. */
function repeatSectionSpec(properties: Record<string, unknown> = {}) {
  return {
    display: "form",
    components: [
      { type: "number", key: "childCount", label: "How many children?", input: true },
      {
        type: "bcgovRepeatSection",
        key: "children",
        label: "Children",
        input: true,
        properties,
        components: [
          {
            type: "textfield",
            key: "childFirstName",
            label: "What is this child's first name?",
            input: true,
          },
        ],
      },
      { type: "button", key: "submit", label: "Submit", action: "submit", input: true },
    ],
  } as FormSpecPayload["spec"];
}

const idTaken = () =>
  new SpecRejectedError(409, [
    {
      field: "newVersion",
      keyword: VERSION_TAKEN,
      message: 'A form with the ID "test-intake" already exists.',
    },
  ]);

function specKeys(spec: object): unknown[] {
  const { components } = spec as { components?: { key?: unknown }[] };
  return (components ?? []).map((c) => c.key);
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}

async function waitForTemplateInBuilder() {
  await vi.waitFor(() => {
    expect(
      document.querySelector('[class~="formio-component-submit"]'),
    ).not.toBeNull();
  });
}

// Deleting a component is a builder edit that opens no settings dialog.
async function removeInBuilder(key: string) {
  const selector = `[class~="formio-component-${key}"]`;
  const remove = await vi.waitFor(() => {
    const found = document
      .querySelector(selector)
      ?.closest(".builder-component")
      ?.querySelector<HTMLElement>('[ref="removeComponent"]');
    expect(found, "no remove control on the component").toBeTruthy();
    return found!;
  });
  remove.click();
  await vi.waitFor(() => {
    expect(document.querySelector(selector)).toBeNull();
  });
}

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

function newQueryClient() {
  return new QueryClient({
    // useDraft sets its own retry, which overrides `retry: false` here; zero
    // delay keeps its retries from adding seconds of backoff to the suite.
    defaultOptions: { queries: { retry: false, retryDelay: 0 } },
  });
}

// A re-read that changes nothing on screen can only be awaited in the cache.
async function waitForDraftRead(queryClient: QueryClient, formSpecId: string) {
  await vi.waitFor(() => {
    expect(queryClient.getQueryState(["admin-draft", formSpecId])?.status).toBe(
      "success",
    );
  });
}

function renderPage(
  formSpecId = "test-form",
  search = "",
  queryClient = newQueryClient(),
) {
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter
        initialEntries={[`/form-management/${formSpecId}${search}`]}
      >
        <Routes>
          <Route
            path="/form-management/:formSpecId"
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
      effectiveDate: "2026-10-02",
      incomeRows: [
        {
          familySize: 1,
          a: 0,
          b: 1060,
          c: 0,
          d: 0,
          e: 1360,
          f: 0,
          g: 1535.5,
          h: 0,
          i: 0,
        },
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

    const screen = await renderPage("test-intake", "?new=1&title=Test+Intake");

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
    // Create-only: refused rather than written over a form with this ID.
    expect(input.newVersion).toBe(1);
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
    expect(input.newVersion).toBeUndefined();
  });

  it("shows a new form's refused first save as text and keeps the edits", async () => {
    mockGetDraft.mockRejectedValue(notFound());
    mockSaveDraft.mockRejectedValue(idTaken());

    const screen = await renderPage("test-intake", "?new=1&title=Test+Intake");
    await waitForTemplateInBuilder();
    await removeInBuilder("submit");
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
    await expect
      .element(
        screen.getByText('A form with the ID "test-intake" already exists.'),
      )
      .toBeVisible();
    // Its field is the version number, not a component, so nothing to select.
    await expect
      .element(screen.getByRole("button", { name: /already exists/ }))
      .not.toBeInTheDocument();
    // The edits stay in the working copy: a second attempt sends them again.
    await screen.getByRole("button", { name: "Save draft" }).click();
    await vi.waitFor(() => expect(mockSaveDraft).toHaveBeenCalledTimes(2));
    for (const [, input] of mockSaveDraft.mock.calls) {
      expect(input.newVersion).toBe(1);
      expect(specKeys(input.spec)).not.toContain("submit");
    }
  });

  it("keeps a new form create-only when a re-read finds the ID taken", async () => {
    // Returning to the tab re-reads the draft. Finding another admin's form
    // must not switch the editor to it, or turn the save into an update of it.
    mockGetDraft.mockRejectedValueOnce(notFound()).mockResolvedValue({
      ...draft,
      formSpecId: "test-intake",
      version: 1,
      title: "Their Form",
    });
    mockSaveDraft.mockRejectedValue(idTaken());
    const queryClient = newQueryClient();

    const screen = await renderPage(
      "test-intake",
      "?new=1&title=Test+Intake",
      queryClient,
    );
    await waitForTemplateInBuilder();
    window.dispatchEvent(new Event("visibilitychange"));
    await waitForDraftRead(queryClient, "test-intake");

    await expect
      .element(screen.getByRole("heading", { name: "Test Intake", level: 1 }))
      .toBeVisible();
    expect(
      document.querySelector('[class~="formio-component-fullName"]'),
    ).toBeNull();
    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText(/already exists/)).toBeVisible();

    const [, input] = mockSaveDraft.mock.calls[0];
    expect(input.newVersion).toBe(1);
    expect(input.title).toBe("Test Intake");
    expect(specKeys(input.spec)).toEqual(["submit"]);
  });

  it("keeps the builder and its edits open while a save re-reads the draft", async () => {
    const saved: FormSpecPayload = {
      formSpecId: "test-intake",
      version: 1,
      title: "Test Intake",
      spec: { display: "form", components: [] } as FormSpecPayload["spec"],
    };
    const reRead = deferred<FormSpecPayload>();
    mockGetDraft
      .mockRejectedValueOnce(notFound())
      .mockReturnValueOnce(reRead.promise)
      .mockResolvedValue(saved);
    mockSaveDraft.mockResolvedValue(saved);
    const queryClient = newQueryClient();

    const screen = await renderPage(
      "test-intake",
      "?new=1&title=Test+Intake",
      queryClient,
    );
    await waitForTemplateInBuilder();
    await removeInBuilder("submit");
    await screen.getByRole("button", { name: "Save draft" }).click();
    await vi.waitFor(() => expect(mockGetDraft).toHaveBeenCalledTimes(2));

    // The save's re-read is in flight; the editor must not close for it.
    await expect
      .element(screen.getByText("Loading form…"))
      .not.toBeInTheDocument();
    await expect
      .element(screen.getByRole("button", { name: "Save draft" }))
      .toBeVisible();

    reRead.resolve(saved);
    await waitForDraftRead(queryClient, "test-intake");
    await screen.getByRole("button", { name: "Save draft" }).click();
    await vi.waitFor(() => expect(mockSaveDraft).toHaveBeenCalledTimes(2));

    const [, second] = mockSaveDraft.mock.calls[1];
    expect(second.newVersion).toBeUndefined();
    expect(specKeys(second.spec)).not.toContain("submit");
    expect(
      document.querySelector('[class~="formio-component-submit"]'),
    ).toBeNull();
  });

  it("refuses a new form's publish without publishing when the ID is taken", async () => {
    mockGetDraft.mockRejectedValue(notFound());
    mockSaveDraft.mockRejectedValue(idTaken());

    const screen = await renderPage("test-intake", "?new=1&title=Test+Intake");
    await waitForTemplateInBuilder();
    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(
        screen.getByText('A form with the ID "test-intake" already exists.'),
      )
      .toBeVisible();
    expect(mockSaveDraft.mock.calls[0][1].newVersion).toBe(1);
    expect(mockPublishForm).not.toHaveBeenCalled();
  });

  it("publishes a new form as version 1 and retries a failed publish as an ordinary save", async () => {
    const saved: FormSpecPayload = {
      formSpecId: "test-intake",
      version: 1,
      title: "Test Intake",
      spec: { display: "form", components: [] } as FormSpecPayload["spec"],
    };
    mockGetDraft.mockRejectedValueOnce(notFound()).mockResolvedValue(saved);
    mockSaveDraft.mockResolvedValue(saved);
    mockPublishForm
      .mockRejectedValueOnce(new SpecRejectedError(502, []))
      .mockResolvedValue({ version: 1 });

    const screen = await renderPage("test-intake", "?new=1&title=Test+Intake");
    await waitForTemplateInBuilder();
    await screen.getByRole("button", { name: "Publish" }).click();
    await expect
      .element(screen.getByText(/Could not publish this form \(error 502\)/))
      .toBeVisible();

    // Version 1 is stored now, so a second create-only save would be refused.
    await screen.getByRole("button", { name: "Publish" }).click();
    await expect
      .element(screen.getByText("Published version 1."))
      .toBeVisible();

    expect(mockSaveDraft.mock.calls[0][1].newVersion).toBe(1);
    expect(mockSaveDraft.mock.calls[1][1].newVersion).toBeUndefined();
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

  it("does not show field keys and editing a label updates the preview", async () => {
    mockGetDraft.mockResolvedValue(draft);

    const screen = await renderPage();

    await expect
      .element(screen.getByRole("textbox", { name: "Label for fullName" }))
      .toBeVisible();
    await expect
      .element(screen.getByText("fullName", { exact: true }))
      .not.toBeInTheDocument();

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
    expect(input.newVersion).toBeUndefined();
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

  it("names a repeat section without a count question instead of saving", async () => {
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: repeatSectionSpec({ myssRowLabel: "Child Info" }),
    });

    const screen = await renderPage();
    await screen
      .getByRole("textbox", { name: "Label for childFirstName" })
      .fill("First name");
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(
        screen.getByRole("button", {
          name: 'The repeat section "children" needs a count question.',
        }),
      )
      .toBeVisible();
    expect(mockSaveDraft).not.toHaveBeenCalled();
  });

  it("surfaces a 422 as an error summary and keeps the edits on screen", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockRejectedValue(
      new SpecRejectedError(422, [
        {
          field: "fullName",
          keyword: "required",
          message: "Label is required.",
        },
      ]),
    );

    const screen = await renderPage();

    await screen
      .getByRole("textbox", { name: "Label for fullName" })
      .fill("Edited label");
    await screen.getByRole("button", { name: "Publish" }).click();

    // The summary lists the server's reason, as a button to its field...
    await expect.element(screen.getByText("There is a problem")).toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Label is required." }))
      .toBeVisible();
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

  it("shows a save refused on a publish retry, not the earlier publish failure", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft
      .mockResolvedValueOnce({ ...draft, version: 4 })
      .mockRejectedValueOnce(
        new SpecRejectedError(422, [
          {
            field: "fullName",
            keyword: "required",
            message: "Label is required.",
          },
        ]),
      );
    mockPublishForm.mockRejectedValue(new SpecRejectedError(502, []));

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Publish" }).click();
    await expect
      .element(screen.getByText(/Could not publish this form/))
      .toBeVisible();

    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(screen.getByRole("button", { name: "Label is required." }))
      .toBeVisible();
    await expect
      .element(screen.getByText(/Could not publish this form/))
      .not.toBeInTheDocument();
    expect(mockPublishForm).toHaveBeenCalledTimes(1);
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

/**
 * A copy of a type's dialog overrides, which building the edit form mutates.
 * JSON rather than structuredClone: the repeat section's count question builds
 * its list with a function, which structuredClone refuses and these checks do
 * not need.
 */
function copyOverrides(overrides: unknown): unknown {
  return JSON.parse(JSON.stringify(overrides)) as unknown;
}

/** The repeat section keyed "children" on the builder's canvas, once it is drawn. */
function builderSection(): Promise<HTMLElement> {
  return vi.waitFor(() => {
    const found = document.querySelector<HTMLElement>(
      ".builder-component .formio-component-children",
    );
    expect(found, "the section is not on the canvas").not.toBeNull();
    return found!;
  });
}

/** The builder's drop area inside a repeat section, with what Form.io attaches to it. */
function sectionDropArea(section: HTMLElement) {
  const container = section.querySelector('[ref="children-container"]');
  expect(container, "the section has no drop area").not.toBeNull();
  return container as HTMLElement & {
    formioContainer?: { key?: string }[];
    formioComponent?: { component: { key: string } };
  };
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
        copyOverrides(builderOptions.editForm[type]),
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
        component!.editForm!(copyOverrides(builderOptions.editForm[type])),
        "data",
      );
      const withoutOverride = tabFields(
        component!.editForm!(undefined),
        "data",
      );

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
        copyOverrides(builderOptions.editForm[type]),
      );
      expect(tabFields(built, "data"), `${type} gained a Data tab`).toEqual([]);
    }
  });

  it("gives the repeat section a heading, count question and maximum in place of the data grid's row controls", () => {
    const section = registered("bcgovRepeatSection");
    const built = section!.editForm!(
      copyOverrides(builderOptions.editForm.bcgovRepeatSection),
    );
    const display = tabFields(built, "display");

    expect(display).toEqual(
      expect.arrayContaining([
        "properties.myssRowLabel",
        "properties.myssRepeatCount",
        "properties.myssRepeatMax",
      ]),
    );
    for (const control of ["addAnother", "reorder", "initEmpty", "disableAddingRemovingRows"]) {
      expect(display, `${control} is still offered`).not.toContain(control);
    }
    expect(tabFields(built, "data")).not.toContain("defaultValue");
    expect(tabFields(built, "validation")).not.toContain("validate.maxLength");
  });

  it("draws a repeat section stacked in the builder, its drop area holding the section's questions", async () => {
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: repeatSectionSpec({ myssRepeatCount: "childCount", myssRowLabel: "Child Info" }),
    });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    const section = await builderSection();

    expect(section.querySelector("table"), "drawn as a data grid table").toBeNull();
    expect(section.textContent).toContain("Children");
    expect(section.textContent).toContain("Child Info");
    const container = sectionDropArea(section);
    // Questions dropped here go into the section's own components.
    expect(container.formioComponent?.component.key).toBe("children");
    expect(container.formioContainer?.map((c) => c.key)).toEqual(["childFirstName"]);
    expect(container.querySelectorAll(".builder-component")).toHaveLength(1);
  });

  it("shows the builder's drop placeholder in an empty repeat section", async () => {
    const spec = repeatSectionSpec({ myssRepeatCount: "childCount" });
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: {
        ...spec,
        components: (spec.components as Record<string, unknown>[]).map((c) =>
          c.key === "children" ? { ...c, components: [] } : c,
        ),
      } as FormSpecPayload["spec"],
    });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    const section = await builderSection();

    expect(sectionDropArea(section).querySelector(".drag-and-drop-alert")).not.toBeNull();
  });

  it("saves a question removed from inside a repeat section", async () => {
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: repeatSectionSpec({ myssRepeatCount: "childCount" }),
    });
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    const section = await builderSection();
    section.querySelector<HTMLElement>('.builder-component [ref="removeComponent"]')!.click();
    await vi.waitFor(() => {
      expect(document.querySelector('[class~="formio-component-childFirstName"]')).toBeNull();
    });

    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText("Draft saved.")).toBeVisible();

    const [, input] = mockSaveDraft.mock.calls[0];
    const saved = (input.spec.components ?? []) as Record<string, unknown>[];
    expect(saved.find((c) => c.key === "children")?.components).toEqual([]);
    expect(saved.map((c) => c.key)).toEqual(["childCount", "children", "submit"]);
  });

  it("saves a question edited inside a repeat section in its place", async () => {
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: repeatSectionSpec({ myssRepeatCount: "childCount" }),
    });
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    const section = await builderSection();
    section.querySelector<HTMLElement>('.builder-component [ref="editComponent"]')!.click();
    const label = await vi.waitFor(() => {
      const found = document.querySelector<HTMLInputElement>('.formio-dialog input[name="data[label]"]');
      expect(found?.value, "the settings dialog did not open on the question").toBe(
        "What is this child's first name?",
      );
      return found!;
    });
    await page.elementLocator(label).fill("Child's first name");
    document.querySelector<HTMLElement>('[ref="saveButton"]')!.click();
    await vi.waitFor(() => {
      expect(document.querySelector('[ref="saveButton"]')).toBeNull();
    });

    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText("Draft saved.")).toBeVisible();

    const [, input] = mockSaveDraft.mock.calls[0];
    const saved = (input.spec.components ?? []) as Record<string, unknown>[];
    const children = saved.find((c) => c.key === "children")?.components as Record<string, unknown>[];
    expect(children.map((c) => [c.key, c.label])).toEqual([["childFirstName", "Child's first name"]]);
    expect(saved.map((c) => c.key)).toEqual(["childCount", "children", "submit"]);
  });

  it("draws a repeat section in the builder with its questions inside it", async () => {
    mockGetDraft.mockResolvedValue({ ...draft, spec: repeatSectionSpec() });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();

    await vi.waitFor(() => {
      const section = document.querySelector(".builder-component .formio-component-children");
      expect(section, "the section is not on the canvas").not.toBeNull();
      expect(section?.textContent).toContain("What is this child's first name?");
    });
  });

  it("gives the accordion heading and body fields in place of a label", () => {
    const accordion = registered("bcgovAccordion");
    const fields = tabFields(
      accordion!.editForm!(
        copyOverrides(builderOptions.editForm.bcgovAccordion),
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
      expect(
        document.querySelectorAll(".formcomponent").length,
      ).toBeGreaterThan(0);
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

    // The builder rebuilds its canvas asynchronously after a removal; the save
    // must read the spec after that, not race it.
    await vi.waitFor(() => {
      expect(
        document.querySelector('[class~="formio-component-fullName"]'),
      ).toBeNull();
    });

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

  it("saves the builder's live schema when the save follows the edit at once", async () => {
    // The builder announces an edit only after its canvas rebuild settles. A
    // save clicked straight after the edit must still send the edited spec.
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();

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
    const keys = ((input.spec.components ?? []) as { key?: unknown }[]).map(
      (c) => c.key,
    );
    expect(keys).not.toContain("fullName");
    expect(keys).toContain("conditionalField");
  });

  it("drops a property whose value equals Form.io's default", () => {
    // Characterises a known fidelity loss rather than endorsing it: the
    // builder's schema omits any property already at its default, so a button
    // loses action:"submit". Harmless at render — Form.io re-applies the
    // default — but it means a save rewrites part of an untouched component.
    // If this assertion starts failing, the loss has been fixed; delete it.
    const button = registered("button");
    const defaults = button?.builderInfo?.schema as
      { action?: string } | undefined;

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

    await expect.element(screen.getByText("Do you have status?")).toBeVisible();
    await expect
      .element(screen.getByText("What does status mean?"))
      .toBeVisible();
  });

  it("keeps the settings dialog working in a builder opened a second time", async () => {
    // Each builder needs its own options object: Form.io stores its event
    // emitter on the one it is given, and the next builder would share it
    // along with the first builder's listeners.
    mockGetDraft.mockResolvedValue(draft);

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    await screen.getByRole("button", { name: "Edit labels" }).click();
    await screen.getByRole("button", { name: "Build form" }).click();

    const edit = await vi.waitFor(() => {
      const found = document
        .querySelector(
          '.builder-component [class~="formio-component-fullName"]',
        )
        ?.closest(".builder-component")
        ?.querySelector<HTMLElement>('[ref="editComponent"]');
      expect(found, "no edit control on the component").toBeTruthy();
      return found!;
    });
    edit.click();
    // Save validates the dialog's own form, so wait until it holds the field.
    await vi.waitFor(() => {
      expect(
        document.querySelector<HTMLInputElement>(
          '.formio-dialog input[name="data[label]"]',
        )?.value,
        "the settings dialog did not open on the field",
      ).toBe("Full name");
    });
    document.querySelector<HTMLElement>('[ref="saveButton"]')!.click();

    await vi.waitFor(() => {
      expect(document.querySelector('[ref="saveButton"]')).toBeNull();
    });
    expect(builderOptions).not.toHaveProperty("events");
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

describe("FormEditorPage form layout", () => {
  // The design system covers the radio input with its indicator, so a user
  // (and the test) clicks the label.
  function chooseLayout(
    screen: Awaited<ReturnType<typeof renderPage>>,
    name: string,
  ) {
    return screen.getByText(name, { exact: true }).click();
  }

  it("saves a form switched to step by step as a wizard with its fields on a first page", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    const wizard = screen.getByRole("radio", { name: "Step by step (wizard)" });
    await expect
      .element(screen.getByRole("radio", { name: "All on one page" }))
      .toBeChecked();
    await chooseLayout(screen, "Step by step (wizard)");
    await expect.element(wizard).toBeChecked();
    await screen.getByRole("button", { name: "Save draft" }).click();

    await vi.waitFor(() => expect(mockSaveDraft).toHaveBeenCalledTimes(1));
    const spec = mockSaveDraft.mock.calls[0][1].spec as unknown as {
      display: string;
      components: { type: string; components?: { key: string }[] }[];
    };
    expect(spec.display).toBe("wizard");
    expect(spec.components).toHaveLength(1);
    expect(spec.components[0].type).toBe("panel");
    expect(spec.components[0].components?.map((c) => c.key)).toEqual([
      "fullName",
      "conditionalField",
      "help",
    ]);
  });

  it("switches the open builder to pages and saves its wizard schema", async () => {
    mockGetDraft.mockResolvedValue(draft);
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    await expect.element(screen.getByText("Full name")).toBeVisible();

    await chooseLayout(screen, "Step by step (wizard)");

    // The wizard builder lists its pages above the drop area.
    await vi.waitFor(() => {
      expect(document.querySelector(".wizard-pages")).not.toBeNull();
    });
    await expect.element(screen.getByText("Full name")).toBeVisible();

    await screen.getByRole("button", { name: "Save draft" }).click();
    await vi.waitFor(() => expect(mockSaveDraft).toHaveBeenCalledTimes(1));
    const spec = mockSaveDraft.mock.calls[0][1].spec as unknown as {
      display: string;
      components: { type: string }[];
    };
    expect(spec.display).toBe("wizard");
    expect(spec.components.map((c) => c.type)).toEqual(["panel"]);
  });
});

describe("FormEditorPage channel rules", () => {
  const sin = {
    type: "textfield",
    key: "sin",
    label: "Social insurance number",
    input: true,
    validate: { required: true },
    properties: {
      myssValidator: "sin",
      myssChannels: { "worker-override": { required: false } },
    },
  };
  const clientNote = {
    type: "textfield",
    key: "clientNote",
    label: "Asked of clients only",
    input: true,
    properties: { myssChannels: { "worker-override": { show: false } } },
  };
  const fullName = {
    type: "textfield",
    key: "fullName",
    label: "Full name",
    input: true,
  };
  const submit = {
    type: "button",
    key: "submit",
    label: "Submit",
    action: "submit",
    input: true,
  };

  function withComponents(...components: object[]): FormSpecPayload {
    return {
      ...draft,
      spec: { display: "form", components } as FormSpecPayload["spec"],
    };
  }

  // The design system covers the radio input with its indicator, so a user
  // (and the test) clicks the label.
  function previewAs(
    screen: Awaited<ReturnType<typeof renderPage>>,
    name: string,
  ) {
    return screen.getByText(name, { exact: true }).click();
  }

  // Each row renders on its own, so wait for the field itself, then read its mark.
  async function expectRequiredMark(key: string, label: string, marked: boolean) {
    await vi.waitFor(() => {
      const text =
        document.querySelector(`[class~="formio-component-${key}"]`)
          ?.textContent ?? "";
      expect(text).toContain(label);
      expect(text.includes("(required)")).toBe(marked);
    });
  }

  async function savedComponents(
    screen: Awaited<ReturnType<typeof renderPage>>,
  ) {
    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(screen.getByText("Draft saved.")).toBeVisible();
    const [, input] = mockSaveDraft.mock.calls[0];
    return (input.spec.components ?? []) as Record<string, unknown>[];
  }

  function channelRulesOf(
    components: Record<string, unknown>[],
    key: string,
  ): unknown {
    const properties = components.find((c) => c.key === key)?.properties as
      | Record<string, unknown>
      | undefined;
    return properties?.myssChannels;
  }

  // Opens the component's settings on the Channel rules tab.
  async function openChannelRules(key: string) {
    const edit = await vi.waitFor(() => {
      const found = document
        .querySelector(`.builder-component [class~="formio-component-${key}"]`)
        ?.closest(".builder-component")
        ?.querySelector<HTMLElement>('[ref="editComponent"]');
      expect(found, "no edit control on the component").toBeTruthy();
      return found!;
    });
    edit.click();
    const tab = await vi.waitFor(() => {
      const found = [
        ...document.querySelectorAll<HTMLElement>(".formio-dialog .nav-link"),
      ].find((link) => link.textContent?.trim() === "Channel rules");
      expect(found, "no Channel rules tab").toBeTruthy();
      return found!;
    });
    await page.elementLocator(tab).click();
  }

  function channelFieldset(channel: string): HTMLElement {
    const legend = [
      ...document.querySelectorAll(".formio-dialog legend"),
    ].find((node) => node.textContent?.trim() === channel);
    expect(legend, `no ${channel} section`).toBeTruthy();
    return legend!.closest("fieldset")!;
  }

  /** The labels of the choices a channel's section shows. */
  function shownChoices(channel: string): string[] {
    return [...channelFieldset(channel).querySelectorAll(".formio-component-select")]
      .filter((select) => !select.classList.contains("formio-hidden"))
      .map((select) => select.querySelector("label")?.textContent?.trim() ?? "");
  }

  function choiceIn(channel: string, setting: string): HTMLElement {
    const found = [
      ...channelFieldset(channel).querySelectorAll<HTMLElement>(
        ".formio-component-select",
      ),
    ].find((select) => select.querySelector("label")?.textContent?.trim() === setting);
    expect(found, `no ${setting} choice for ${channel}`).toBeTruthy();
    return found!.querySelector<HTMLElement>(".choices")!;
  }

  async function choose(channel: string, setting: string, option: string) {
    const choice = choiceIn(channel, setting);
    await page.elementLocator(choice).click();
    await page.elementLocator(choice).getByText(option, { exact: true }).click();
  }

  async function saveSettings() {
    document.querySelector<HTMLElement>('[ref="saveButton"]')!.click();
    await vi.waitFor(() => {
      expect(document.querySelector('[ref="saveButton"]')).toBeNull();
    });
  }

  it("previews the form as each channel gets it", async () => {
    mockGetDraft.mockResolvedValue(withComponents(sin, clientNote, fullName, submit));

    const screen = await renderPage();
    await expect
      .element(screen.getByRole("radio", { name: "Base form" }))
      .toBeChecked();
    await expect.element(screen.getByText("Asked of clients only")).toBeVisible();
    await expectRequiredMark("sin", "Social insurance number", true);

    await previewAs(screen, "Worker override");
    await expect
      .element(screen.getByText("Asked of clients only"))
      .not.toBeInTheDocument();
    await expectRequiredMark("sin", "Social insurance number", false);
    // The settings still edit the form as authored.
    await expect
      .element(screen.getByRole("checkbox", { name: "Required: sin" }))
      .toBeChecked();

    await previewAs(screen, "Client");
    await expect.element(screen.getByText("Asked of clients only")).toBeVisible();
    await expectRequiredMark("sin", "Social insurance number", true);
  });

  it("previews a wizard page hidden for a channel without its questions", async () => {
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: {
        display: "wizard",
        components: [
          { type: "panel", key: "page1", title: "Page 1", input: false, components: [fullName] },
          {
            type: "panel",
            key: "page2",
            title: "Page 2",
            input: false,
            properties: { myssChannels: { "worker-override": { show: false } } },
            components: [{ ...clientNote, properties: {} }],
          },
        ],
      } as unknown as FormSpecPayload["spec"],
    });

    const screen = await renderPage();
    await expect.element(screen.getByText("Asked of clients only")).toBeVisible();
    await expect
      .element(screen.getByText('Hidden for Worker override (inside "Page 2")'))
      .toBeVisible();

    await previewAs(screen, "Worker override");

    await expect
      .element(screen.getByText("Asked of clients only"))
      .not.toBeInTheDocument();
    await expect
      .element(screen.getByRole("textbox", { name: "Label for fullName" }))
      .toBeVisible();
  });

  it("says beside each field what its channel rules do", async () => {
    mockGetDraft.mockResolvedValue(withComponents(sin, clientNote, fullName, submit));

    const screen = await renderPage();

    await expect
      .element(screen.getByText("Optional for Worker override"))
      .toBeVisible();
    await expect
      .element(screen.getByText("Hidden for Worker override"))
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: "Label for sin" }))
      .toHaveAccessibleDescription("Optional for Worker override");
  });

  it("links an error only to a field the preview shows", async () => {
    const message =
      'The channel rule of "clientNote" for "client" has "visible"; only show and required are allowed.';
    mockGetDraft.mockResolvedValue(
      withComponents(
        {
          ...clientNote,
          properties: {
            myssChannels: {
              client: { visible: true },
              "worker-override": { show: false },
            },
          },
        },
        fullName,
        submit,
      ),
    );

    const screen = await renderPage();
    await previewAs(screen, "Worker override");
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect.element(screen.getByText(message)).toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: message }))
      .not.toBeInTheDocument();

    await previewAs(screen, "Base form");
    await expect
      .element(screen.getByRole("button", { name: message }))
      .toBeVisible();
  });

  it("names a rule for a channel that does not exist instead of saving", async () => {
    mockGetDraft.mockResolvedValue(
      withComponents(
        {
          ...sin,
          properties: { myssChannels: { "worker-overide": { required: false } } },
        },
        submit,
      ),
    );

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(
        screen.getByRole("button", {
          name: 'The channel rules of "sin" name "worker-overide", which is not a channel.',
        }),
      )
      .toBeVisible();
    expect(mockSaveDraft).not.toHaveBeenCalled();
  });

  it("saves a choice made on the Channel rules tab, and no rule where none is made", async () => {
    mockGetDraft.mockResolvedValue(withComponents(sin, clientNote, fullName, submit));
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    await openChannelRules("fullName");
    expect(shownChoices("Worker override")).toEqual(["Visibility", "Required"]);
    await choose("Worker override", "Required", "Optional");
    await saveSettings();

    const saved = await savedComponents(screen);
    expect(channelRulesOf(saved, "fullName")).toEqual({
      "worker-override": { required: false },
    });
    expect(channelRulesOf(saved, "sin")).toEqual({
      "worker-override": { required: false },
    });
    expect(channelRulesOf(saved, "clientNote")).toEqual({
      "worker-override": { show: false },
    });
  });

  it("saves a component's settings unchanged without touching its channel rules", async () => {
    mockGetDraft.mockResolvedValue(withComponents(sin, fullName, submit));
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    await openChannelRules("sin");
    await saveSettings();
    await openChannelRules("fullName");
    await saveSettings();
    await openChannelRules("submit");
    await saveSettings();

    const saved = await savedComponents(screen);
    expect(channelRulesOf(saved, "sin")).toEqual({
      "worker-override": { required: false },
    });
    expect(channelRulesOf(saved, "fullName")).toBeUndefined();
    // Nothing else of the button carries properties, so none are left behind.
    expect(saved.find((c) => c.key === "submit")).not.toHaveProperty("properties");
  });

  it("drops a rule whose choice is cleared back to As authored", async () => {
    mockGetDraft.mockResolvedValue(withComponents(sin, submit));
    mockSaveDraft.mockResolvedValue({ ...draft, version: 3 });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    await openChannelRules("sin");
    const clear = await vi.waitFor(() => {
      const found = choiceIn("Worker override", "Required").querySelector<HTMLElement>(
        ".choices__button",
      );
      expect(found, "the choice shows no clear button").toBeTruthy();
      return found!;
    });
    await page.elementLocator(clear).click();
    await saveSettings();

    expect(channelRulesOf(await savedComponents(screen), "sin")).toBeUndefined();
  });

  it("asks only whether a repeat section's count question is hidden", async () => {
    const spec = repeatSectionSpec({ myssRepeatCount: "childCount" }) as {
      components: object[];
    };
    mockGetDraft.mockResolvedValue({
      ...draft,
      spec: {
        ...spec,
        components: [
          { type: "number", key: "petCount", label: "How many pets?", input: true },
          ...spec.components,
        ],
      } as unknown as FormSpecPayload["spec"],
    });

    const screen = await renderPage();
    await screen.getByRole("button", { name: "Build form" }).click();
    await openChannelRules("petCount");
    await vi.waitFor(() => {
      expect(shownChoices("Worker override")).toEqual(["Visibility", "Required"]);
    });
    await saveSettings();

    await openChannelRules("childCount");
    await vi.waitFor(() => {
      expect(shownChoices("Client")).toEqual(["Visibility"]);
      expect(shownChoices("Worker override")).toEqual(["Visibility"]);
    });
  });
});
