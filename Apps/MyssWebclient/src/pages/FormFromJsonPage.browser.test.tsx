import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createBrowserRouter,
  createMemoryRouter,
  RouterProvider,
} from "react-router";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import FormFromJsonPage from "./FormFromJsonPage";
import {
  FormLoadError,
  SpecRejectedError,
  VERSION_TAKEN,
  getFormVersion,
  listForms,
  publishForm,
  saveDraft,
} from "@/api/forms";
import type { FormSpecPayload, FormSummary, SaveDraftInput } from "@/api/forms";
import { paths } from "@/routes/paths";
import { EXAMPLE_SPEC } from "@/widgets/formio/specImport";

// The page against a stubbed admin API: only the calls are mocked, and the
// real error classes are kept. A data router hosts it, because the leave
// guard needs one.

vi.mock("@/api/forms", async (importActual) => ({
  ...(await importActual<typeof import("@/api/forms")>()),
  listForms: vi.fn(),
  getFormVersion: vi.fn(),
  saveDraft: vi.fn(),
  publishForm: vi.fn(),
}));

const mockListForms = vi.mocked(listForms);
const mockGetFormVersion = vi.mocked(getFormVersion);
const mockSaveDraft = vi.mocked(saveDraft);
const mockPublishForm = vi.mocked(publishForm);

type Screen = Awaited<ReturnType<typeof render>>;

const BUS_PASS_PUBLISHED: FormSummary = {
  formSpecId: "bc-bus-pass",
  title: "BC Bus Pass",
  versions: [
    { version: 7, isPublished: true },
    { version: 8, isPublished: true },
  ],
};

const BUS_PASS_WITH_DRAFT: FormSummary = {
  ...BUS_PASS_PUBLISHED,
  versions: [
    ...BUS_PASS_PUBLISHED.versions,
    { version: 9, isPublished: false },
  ],
};

const BUS_PASS_V8: FormSpecPayload = {
  formSpecId: "bc-bus-pass",
  version: 8,
  title: "BC Bus Pass",
  spec: {
    display: "form",
    components: [
      {
        type: "textfield",
        key: "firstName",
        label: "First name",
        input: true,
      },
      { type: "email", key: "email", label: "Email", input: true },
      {
        type: "button",
        key: "submit",
        label: "Submit",
        action: "submit",
        input: true,
      },
    ],
  } as FormSpecPayload["spec"],
};

const REGISTRATION: FormSummary = {
  formSpecId: "registration",
  title: "Registration",
  versions: [{ version: 5, isPublished: true }],
};

// A spec using the older radio type, which only an existing form that already
// uses it may keep.
const WITH_BCGOV_RADIO = {
  display: "form",
  components: [
    {
      type: "bcgovRadio",
      key: "status",
      label: "Status",
      input: true,
      values: [
        { label: "Citizen", value: "citizen" },
        { label: "Resident", value: "resident" },
      ],
    },
    {
      type: "button",
      key: "submit",
      label: "Submit",
      action: "submit",
      input: true,
    },
  ],
};

const REGISTRATION_V5: FormSpecPayload = {
  formSpecId: "registration",
  version: 5,
  title: "Registration",
  spec: WITH_BCGOV_RADIO as FormSpecPayload["spec"],
};

const EXAMPLE_TEXT = JSON.stringify(EXAMPLE_SPEC, null, 2);

function withoutComponent(spec: object, key: string): object {
  const { components } = spec as { components: { key: string }[] };
  return {
    ...spec,
    components: components.filter((component) => component.key !== key),
  };
}

function lineOf(text: string, offset: number): number {
  return text.slice(0, offset).split("\n").length;
}

// The check and the preview update once typing pauses, above the action
// buttons; waiting for both keeps a following click from landing on the
// preview as it grows.
async function pasteValid(screen: Screen, text: string, previewField: string) {
  await jsonBody(screen).fill(text);
  await expect.element(jsonBody(screen)).toHaveAccessibleDescription(/Valid\./);
  await expect
    .element(screen.getByRole("textbox", { name: previewField }))
    .toBeVisible();
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<T>((settle, fail) => {
    resolve = settle;
    reject = fail;
  });
  return { promise, resolve, reject };
}

function newQueryClient() {
  return new QueryClient({
    // The version read sets its own retry, which overrides `retry: false`
    // here; zero delay keeps its retries from slowing the suite.
    defaultOptions: { queries: { retry: false, retryDelay: 0 } },
  });
}

const routes = [
  { path: paths.adminFormFromJson, element: <FormFromJsonPage /> },
  {
    path: paths.adminFormManagement,
    element: <h1>Form management list</h1>,
  },
];

async function renderPage(url: string, queryClient = newQueryClient()) {
  const router = createMemoryRouter(routes, { initialEntries: [url] });
  const screen = await render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { screen, router };
}

function jsonBody(screen: Screen) {
  return screen.getByRole("textbox", { name: "Form JSON" });
}

// Opens the page with its text box: for a new form, or for the next version
// of an existing one after its confirmation.
async function openPage(
  existing?: { form: FormSummary; current: FormSpecPayload },
  queryClient = newQueryClient(),
) {
  mockListForms.mockResolvedValue(existing ? [existing.form] : []);
  if (existing) mockGetFormVersion.mockResolvedValue(existing.current);
  const url = existing
    ? `/admin/form-management/${existing.form.formSpecId}/from-json?confirm=new-${existing.current.version + 1}`
    : "/admin/form-management/zz-free-id/from-json?title=Check";
  const page = await renderPage(url, queryClient);
  await expect.element(jsonBody(page.screen)).toBeVisible();
  return page;
}

afterEach(() => {
  vi.clearAllMocks();
});

describe("FormFromJsonPage: save target", () => {
  it("opens the text box for an ID no form has", async () => {
    mockListForms.mockResolvedValue([BUS_PASS_PUBLISHED]);

    const { screen } = await renderPage(
      "/admin/form-management/zz-free-id/from-json?title=Check",
    );

    await expect.element(jsonBody(screen)).toBeVisible();
    await expect
      .element(screen.getByRole("heading", { level: 1 }))
      .toHaveTextContent("Check");
    await expect.element(screen.getByText(/New form/)).toBeVisible();
    expect(mockGetFormVersion).not.toHaveBeenCalled();
  });

  it("opens the text box for a confirmed new version and reads the current version", async () => {
    mockListForms.mockResolvedValue([BUS_PASS_PUBLISHED]);
    mockGetFormVersion.mockResolvedValue(BUS_PASS_V8);

    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json?confirm=new-9",
    );

    await expect.element(jsonBody(screen)).toBeVisible();
    await expect.element(screen.getByText(/New version 9/)).toBeVisible();
    expect(mockGetFormVersion).toHaveBeenCalledWith("bc-bus-pass", 8);
  });

  it("asks before opening an existing form, and records the answer in the URL", async () => {
    mockListForms.mockResolvedValue([BUS_PASS_PUBLISHED]);
    mockGetFormVersion.mockResolvedValue(BUS_PASS_V8);

    const { screen, router } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json",
    );

    const heading = screen.getByRole("heading", {
      name: 'Create a new version of "BC Bus Pass"?',
    });
    await expect.element(heading).toBeVisible();
    await expect.element(heading).toHaveFocus();
    await expect.element(jsonBody(screen)).not.toBeInTheDocument();

    await screen.getByRole("button", { name: "Create version 9" }).click();

    await expect.element(jsonBody(screen)).toBeVisible();
    expect(router.state.location.search).toContain("confirm=new-9");
    await expect.element(heading).not.toBeInTheDocument();
  });

  it("returns to Form management when the warning is cancelled", async () => {
    mockListForms.mockResolvedValue([BUS_PASS_PUBLISHED]);

    const { screen, router } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json",
    );

    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect
      .element(screen.getByRole("heading", { name: "Form management list" }))
      .toBeVisible();
    expect(router.state.location.pathname).toBe(paths.adminFormManagement);
  });

  it("asks again when the form changed after the confirmation", async () => {
    mockListForms.mockResolvedValue([BUS_PASS_WITH_DRAFT]);

    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json?confirm=new-9",
    );

    await expect
      .element(
        screen.getByRole("heading", {
          name: 'Replace the unpublished draft of "BC Bus Pass"?',
        }),
      )
      .toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Replace draft version 9" }))
      .toBeVisible();
    await expect.element(jsonBody(screen)).not.toBeInTheDocument();
  });

  it("waits for a read made after it opened, not the list cached before", async () => {
    const queryClient = newQueryClient();
    queryClient.setQueryData(["admin-forms"], []);
    const read = deferred<FormSummary[]>();
    mockListForms.mockReturnValue(read.promise);

    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json",
      queryClient,
    );

    await expect
      .element(screen.getByText("Checking…", { exact: true }))
      .toBeVisible();
    await expect.element(jsonBody(screen)).not.toBeInTheDocument();

    read.resolve([BUS_PASS_PUBLISHED]);

    await expect
      .element(
        screen.getByRole("heading", {
          name: 'Create a new version of "BC Bus Pass"?',
        }),
      )
      .toBeVisible();
    await expect.element(jsonBody(screen)).not.toBeInTheDocument();
  });

  it("shows an existing form's own title, not the one in the URL", async () => {
    mockListForms.mockResolvedValue([BUS_PASS_PUBLISHED]);
    mockGetFormVersion.mockResolvedValue(BUS_PASS_V8);

    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json?confirm=new-9&title=Renamed",
    );

    await expect.element(jsonBody(screen)).toBeVisible();
    await expect
      .element(screen.getByRole("heading", { level: 1 }))
      .toHaveTextContent("BC Bus Pass");
    expect(screen.getByText("Renamed").query()).toBeNull();
  });

  it("shows the error when the forms list cannot be read", async () => {
    mockListForms.mockRejectedValue(new Error("Forms list failed (500)"));

    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json",
    );

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Forms list failed (500)");
    await expect.element(jsonBody(screen)).not.toBeInTheDocument();
  });

  it("checks again when Try again is selected after a failed read", async () => {
    mockListForms
      .mockRejectedValueOnce(new Error("Forms list failed (500)"))
      .mockResolvedValue([]);

    const { screen } = await renderPage(
      "/admin/form-management/zz-free-id/from-json",
    );

    await screen.getByRole("button", { name: "Try again" }).click();

    await expect.element(jsonBody(screen)).toBeVisible();
    expect(mockListForms).toHaveBeenCalledTimes(2);
  });

  it("shows the error when the current version cannot be read", async () => {
    mockListForms.mockResolvedValue([BUS_PASS_PUBLISHED]);
    mockGetFormVersion.mockRejectedValue(
      new FormLoadError(404, "Version fetch failed (404)"),
    );

    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json?confirm=new-9",
    );

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Could not load version 8 of this form");
    await expect.element(jsonBody(screen)).not.toBeInTheDocument();
  });

  it("refuses an invalid form ID without a request", async () => {
    const { screen } = await renderPage(
      "/admin/form-management/Bad-ID/from-json",
    );

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("does not contain a valid form ID");
    expect(mockListForms).not.toHaveBeenCalled();
    expect(mockGetFormVersion).not.toHaveBeenCalled();
  });
});

describe("FormFromJsonPage: JSON, checks and preview", () => {
  const BUS_PASS = { form: BUS_PASS_PUBLISHED, current: BUS_PASS_V8 };

  it("inserts the example and previews it", async () => {
    const { screen } = await openPage();

    await screen.getByRole("button", { name: "Insert example" }).click();

    await expect
      .element(screen.getByRole("textbox", { name: "Your full name" }))
      .toBeVisible();
    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(/Valid\. 3 questions\./);
    await expect.element(jsonBody(screen)).toHaveFocus();
  });

  it("asks before the example replaces text in the box", async () => {
    const { screen } = await openPage();
    await jsonBody(screen).fill("{");

    await screen.getByRole("button", { name: "Insert example" }).click();
    const question = screen.getByRole("heading", {
      name: "Replace the text in the box with the example?",
    });
    await expect.element(question).toHaveFocus();

    await screen.getByRole("button", { name: "Keep my text" }).click();
    await expect.element(jsonBody(screen)).toHaveValue("{");
    await expect.element(jsonBody(screen)).toHaveFocus();

    await screen.getByRole("button", { name: "Insert example" }).click();
    await screen.getByRole("button", { name: "Replace" }).click();
    await expect.element(jsonBody(screen)).toHaveValue(EXAMPLE_TEXT);
  });

  it("reports broken JSON with its line, and keeps the last valid preview", async () => {
    const { screen } = await openPage();
    await jsonBody(screen).fill(EXAMPLE_TEXT);
    await expect
      .element(screen.getByRole("textbox", { name: "Your full name" }))
      .toBeVisible();

    await jsonBody(screen).fill(
      EXAMPLE_TEXT.replace('"applicantName",', '"applicantName"'),
    );

    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(/The JSON is not valid at line \d+/);
    await expect
      .element(screen.getByText("Showing the last valid version."))
      .toBeVisible();
    await expect
      .element(screen.getByRole("textbox", { name: "Your full name" }))
      .toBeVisible();
  });

  it("lists a type that is not allowed, naming its key", async () => {
    const { screen } = await openPage();

    await jsonBody(screen).fill(
      EXAMPLE_TEXT.replace('"type": "textfield"', '"type": "datagrid"'),
    );

    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toBeVisible();
    await expect
      .element(
        screen.getByRole("button", {
          name: '"applicantName": the type "datagrid" is not allowed.',
        }),
      )
      .toBeVisible();
    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(/1 problem\. Fix it to save\./);
  });

  it("accepts the types the current version already uses", async () => {
    const { screen } = await openPage({
      form: REGISTRATION,
      current: REGISTRATION_V5,
    });

    await jsonBody(screen).fill(JSON.stringify(WITH_BCGOV_RADIO, null, 2));

    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(/Valid\. 1 question\./);
    expect(screen.getByText("There is a problem").query()).toBeNull();
  });

  it("refuses the older radio type for a new form", async () => {
    const { screen } = await openPage();

    await jsonBody(screen).fill(JSON.stringify(WITH_BCGOV_RADIO, null, 2));

    await expect
      .element(screen.getByRole("button", { name: /use "radio" instead/ }))
      .toBeVisible();
  });

  it("lists the questions the current version has and the JSON lacks", async () => {
    const { screen } = await openPage(BUS_PASS);

    await jsonBody(screen).fill(
      JSON.stringify(withoutComponent(BUS_PASS_V8.spec, "email"), null, 2),
    );

    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(
        /1 question removed compared with version 8: email\./,
      );
  });

  it("puts the caret on a problem's component when the problem is selected", async () => {
    const { screen } = await openPage();
    const text = EXAMPLE_TEXT.replace('"type": "radio"', '"type": "datagrid"');
    await jsonBody(screen).fill(text);

    await screen
      .getByRole("button", { name: /"inCare": the type "datagrid"/ })
      .click();

    const box = jsonBody(screen).element() as HTMLTextAreaElement;
    await expect.element(jsonBody(screen)).toHaveFocus();
    expect(lineOf(box.value, box.selectionStart)).toBe(
      lineOf(text, text.indexOf('"key": "inCare"')),
    );
  });

  it("puts the caret at a syntax error's line", async () => {
    const { screen } = await openPage();
    const text = EXAMPLE_TEXT.replace('"inCare",', '"inCare"');
    await jsonBody(screen).fill(text);

    await screen.getByRole("button", { name: /not valid at line/ }).click();

    const box = jsonBody(screen).element() as HTMLTextAreaElement;
    await expect.element(jsonBody(screen)).toHaveFocus();
    expect(lineOf(box.value, box.selectionStart)).toBe(
      lineOf(text, text.indexOf('"key": "inCare"')) + 1,
    );
  });

  it("formats single-line JSON, and asks for valid JSON first", async () => {
    const { screen } = await openPage();
    await jsonBody(screen).fill(JSON.stringify(EXAMPLE_SPEC));

    await screen.getByRole("button", { name: "Format" }).click();
    await expect.element(jsonBody(screen)).toHaveValue(EXAMPLE_TEXT);

    await jsonBody(screen).fill("{ broken");
    await screen.getByRole("button", { name: "Format" }).click();
    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(/Fix the JSON before formatting it\./);
    await expect.element(jsonBody(screen)).toHaveValue("{ broken");
  });

  it("keeps focus in the text box while problems appear", async () => {
    const { screen } = await openPage();

    await jsonBody(screen).click();
    await userEvent.keyboard('{{"display": "pdf"}');

    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toBeVisible();
    await expect.element(jsonBody(screen)).toHaveFocus();
  });

  it("lets Tab leave the text box", async () => {
    const { screen } = await openPage();
    await jsonBody(screen).fill(EXAMPLE_TEXT);

    await jsonBody(screen).click();
    await userEvent.keyboard("{Tab}");

    await expect.element(jsonBody(screen)).not.toHaveFocus();
    await expect.element(jsonBody(screen)).toHaveValue(EXAMPLE_TEXT);
  });
});

describe("FormFromJsonPage: saving and leaving", () => {
  const BUS_PASS = { form: BUS_PASS_PUBLISHED, current: BUS_PASS_V8 };
  const BUS_PASS_TEXT = JSON.stringify(BUS_PASS_V8.spec, null, 2);

  // Stores each save as the version it names, or else as `version`.
  function storeSavesAs(version: number) {
    mockSaveDraft.mockImplementation(async (formSpecId, input) => ({
      formSpecId,
      version: input.newVersion ?? version,
      title: input.title ?? null,
      spec: input.spec,
    }));
  }

  function savedInput(call: number): SaveDraftInput {
    return mockSaveDraft.mock.calls[call][1];
  }

  const taken = (message: string) =>
    new SpecRejectedError(409, [
      { field: "newVersion", keyword: VERSION_TAKEN, message },
    ]);

  it.each([
    {
      name: "a new form",
      url: "/admin/form-management/zz-free-id/from-json",
      forms: [] as FormSummary[],
      text: EXAMPLE_TEXT,
      field: "Your full name",
      newVersion: 1,
      stored: 1,
      title: null,
    },
    {
      name: "a new version 9",
      url: "/admin/form-management/bc-bus-pass/from-json?confirm=new-9&title=Renamed",
      forms: [BUS_PASS_PUBLISHED],
      text: BUS_PASS_TEXT,
      field: "First name",
      newVersion: 9,
      stored: 9,
      title: "BC Bus Pass",
    },
    {
      name: "a replaced draft 9",
      url: "/admin/form-management/bc-bus-pass/from-json?confirm=replace-9",
      forms: [BUS_PASS_WITH_DRAFT],
      text: BUS_PASS_TEXT,
      field: "First name",
      newVersion: undefined,
      stored: 9,
      title: "BC Bus Pass",
    },
  ])(
    "first saves $name with the right request, then saves ordinarily",
    async ({ url, forms, text, field, newVersion, stored, title }) => {
      mockListForms.mockResolvedValue(forms);
      mockGetFormVersion.mockResolvedValue(BUS_PASS_V8);
      storeSavesAs(stored);
      const { screen } = await renderPage(url);
      await pasteValid(screen, text, field);

      await screen.getByRole("button", { name: "Save draft" }).click();

      await expect
        .element(screen.getByText(`Draft version ${stored} saved.`))
        .toBeVisible();
      expect(savedInput(0).newVersion).toBe(newVersion);
      expect(savedInput(0).title).toBe(title);

      await jsonBody(screen).fill(`${text}\n`);
      await screen.getByRole("button", { name: "Save draft" }).click();

      await vi.waitFor(() => expect(mockSaveDraft).toHaveBeenCalledTimes(2));
      expect(savedInput(1)).not.toHaveProperty("newVersion");
    },
  );

  it("sends nothing while there are problems, and moves focus to them", async () => {
    const { screen } = await openPage();
    await jsonBody(screen).fill(
      EXAMPLE_TEXT.replace('"type": "textfield"', '"type": "datagrid"'),
    );
    const summary = screen.getByRole("heading", { name: "There is a problem" });
    await expect.element(summary).toBeVisible();
    await expect.element(summary).not.toHaveFocus();

    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect.element(summary).toHaveFocus();
    expect(mockSaveDraft).not.toHaveBeenCalled();
  });

  it("asks for JSON when the box is empty", async () => {
    const { screen } = await openPage();

    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(screen.getByText("Paste the form's JSON before saving."))
      .toBeVisible();
    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
    expect(mockSaveDraft).not.toHaveBeenCalled();
  });

  it("asks before saving without questions the current version has", async () => {
    storeSavesAs(9);
    const { screen } = await openPage(BUS_PASS);
    await pasteValid(
      screen,
      JSON.stringify(withoutComponent(BUS_PASS_V8.spec, "email"), null, 2),
      "First name",
    );

    await screen.getByRole("button", { name: "Save draft" }).click();
    const question = screen.getByRole("heading", {
      name: "A question from version 8 is missing",
    });
    await expect.element(question).toHaveFocus();
    await expect
      .element(screen.getByText(/These questions are in version 8/))
      .toHaveTextContent(/not in your JSON: email\./);
    expect(mockSaveDraft).not.toHaveBeenCalled();

    await screen.getByRole("button", { name: "Keep editing" }).click();
    await expect.element(jsonBody(screen)).toHaveFocus();
    expect(mockSaveDraft).not.toHaveBeenCalled();

    await screen.getByRole("button", { name: "Save draft" }).click();
    await screen.getByRole("button", { name: "Save anyway" }).click();
    await expect
      .element(screen.getByText("Draft version 9 saved."))
      .toBeVisible();
    expect(mockSaveDraft).toHaveBeenCalledTimes(1);

    const withoutBoth = withoutComponent(
      withoutComponent(BUS_PASS_V8.spec, "email"),
      "firstName",
    );
    await jsonBody(screen).fill(JSON.stringify(withoutBoth, null, 2));
    await expect
      .element(screen.getByRole("textbox", { name: "First name" }))
      .not.toBeInTheDocument();
    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect
      .element(
        screen.getByRole("heading", {
          name: "2 questions from version 8 are missing",
        }),
      )
      .toHaveFocus();
    expect(mockSaveDraft).toHaveBeenCalledTimes(1);
  });

  it("keeps the JSON and asks for another ID when the ID is taken meanwhile", async () => {
    const { screen, router } = await openPage();
    mockSaveDraft.mockRejectedValueOnce(
      taken('A form with the ID "zz-free-id" already exists.'),
    );
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");

    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(
        screen.getByText(
          "The ID zz-free-id was taken while you were working. Your JSON is kept. Enter another ID.",
        ),
      )
      .toBeVisible();
    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
    await expect.element(jsonBody(screen)).toHaveValue(EXAMPLE_TEXT);

    const newId = screen.getByRole("textbox", { name: "Form ID" });
    await newId.fill("Not valid");
    await screen.getByRole("button", { name: "Use this ID" }).click();
    await expect
      .element(screen.getByText(/Enter a form ID using only lowercase/))
      .toBeVisible();
    await expect.element(newId).toHaveFocus();

    storeSavesAs(1);
    await screen.getByRole("textbox", { name: "Form ID" }).fill("zz-other");
    await screen.getByRole("button", { name: "Use this ID" }).click();

    await expect.element(jsonBody(screen)).toHaveFocus();
    expect(router.state.location.pathname).toBe(
      "/admin/form-management/zz-other/from-json",
    );
    expect(router.state.location.search).toContain("title=Check");
    await expect.element(jsonBody(screen)).toHaveValue(EXAMPLE_TEXT);

    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(screen.getByText("Draft version 1 saved."))
      .toBeVisible();
    expect(mockSaveDraft).toHaveBeenLastCalledWith(
      "zz-other",
      expect.objectContaining({ newVersion: 1, title: "Check" }),
    );
  });

  it("shows the warning when the new ID belongs to an existing form", async () => {
    const { screen } = await openPage();
    mockSaveDraft.mockRejectedValueOnce(
      taken('A form with the ID "zz-free-id" already exists.'),
    );
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");
    await screen.getByRole("button", { name: "Save draft" }).click();
    mockListForms.mockResolvedValue([BUS_PASS_PUBLISHED]);
    mockGetFormVersion.mockResolvedValue(BUS_PASS_V8);

    await screen.getByRole("textbox", { name: "Form ID" }).fill("bc-bus-pass");
    await screen.getByRole("button", { name: "Use this ID" }).click();

    await expect
      .element(
        screen.getByRole("heading", {
          name: 'Create a new version of "BC Bus Pass"?',
        }),
      )
      .toHaveFocus();
    await expect.element(jsonBody(screen)).toHaveValue(EXAMPLE_TEXT);
  });

  it("shows the new situation when the version is taken meanwhile", async () => {
    mockListForms
      .mockResolvedValueOnce([BUS_PASS_PUBLISHED])
      .mockResolvedValue([BUS_PASS_WITH_DRAFT]);
    mockGetFormVersion.mockResolvedValue(BUS_PASS_V8);
    const { screen, router } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json?confirm=new-9",
    );
    mockSaveDraft.mockRejectedValueOnce(
      taken('Version 9 of "bc-bus-pass" is an unpublished draft.'),
    );
    await pasteValid(screen, BUS_PASS_TEXT, "First name");

    await screen.getByRole("button", { name: "Save draft" }).click();

    const warning = screen.getByRole("heading", {
      name: 'Replace the unpublished draft of "BC Bus Pass"?',
    });
    await expect.element(warning).toHaveFocus();
    await expect
      .element(
        screen.getByRole("region", {
          name: 'Replace the unpublished draft of "BC Bus Pass"?',
        }),
      )
      .toHaveTextContent(
        "Version 9 of bc-bus-pass was saved by someone else while you were working. Your JSON is kept.",
      );

    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(warning).toHaveFocus();
    expect(mockSaveDraft).toHaveBeenCalledTimes(1);

    storeSavesAs(9);
    await screen
      .getByRole("button", { name: "Replace draft version 9" })
      .click();

    await expect.element(jsonBody(screen)).toHaveFocus();
    expect(router.state.location.search).toContain("confirm=replace-9");
    await expect.element(jsonBody(screen)).toHaveValue(BUS_PASS_TEXT);
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(screen.getByText("Draft version 9 saved."))
      .toBeVisible();
    expect(savedInput(1)).not.toHaveProperty("newVersion");
  });

  it("shows a refusal with the API's reasons", async () => {
    const { screen } = await openPage();
    mockSaveDraft.mockRejectedValueOnce(
      new SpecRejectedError(422, [
        {
          field: "applicantName",
          keyword: "FORMSPEC.KEY.INVALID",
          message: "The key applicantName is not allowed.",
        },
      ]),
    );
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");

    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
    await screen
      .getByRole("button", { name: "The key applicantName is not allowed." })
      .click();
    await expect.element(jsonBody(screen)).toHaveFocus();
  });

  it("publishes after saving, and creates the next version on the next save", async () => {
    storeSavesAs(1);
    mockPublishForm.mockResolvedValue({ version: 1 });
    const { screen } = await openPage();
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");

    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(screen.getByText("Published version 1."))
      .toBeVisible();
    expect(mockSaveDraft.mock.invocationCallOrder[0]).toBeLessThan(
      mockPublishForm.mock.invocationCallOrder[0],
    );
    await expect.element(screen.getByText(/Version 1 published/)).toBeVisible();

    storeSavesAs(2);
    await jsonBody(screen).fill(`${EXAMPLE_TEXT}\n`);
    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(screen.getByText("Draft version 2 saved."))
      .toBeVisible();
    expect(savedInput(1).newVersion).toBe(2);
  });

  it("leaves at once with an empty box", async () => {
    const { screen, router } = await openPage();

    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect
      .element(screen.getByRole("heading", { name: "Form management list" }))
      .toBeVisible();
    expect(router.state.location.pathname).toBe(paths.adminFormManagement);
  });

  it("asks before discarding JSON that was never saved", async () => {
    const { screen, router } = await openPage();
    await jsonBody(screen).fill("{");
    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toBeVisible();

    await screen.getByRole("button", { name: "Cancel" }).click();
    const question = screen.getByRole("heading", {
      name: "Discard this JSON? Nothing has been saved.",
    });
    await expect.element(question).toHaveFocus();

    await screen.getByRole("button", { name: "Keep editing" }).click();
    await expect.element(jsonBody(screen)).toHaveFocus();
    await expect.element(jsonBody(screen)).toHaveValue("{");
    expect(router.state.location.pathname).not.toBe(paths.adminFormManagement);

    await screen.getByRole("button", { name: "Cancel" }).click();
    await screen.getByRole("button", { name: "Discard" }).click();

    await expect
      .element(screen.getByRole("heading", { name: "Form management list" }))
      .toBeVisible();
    expect(mockSaveDraft).not.toHaveBeenCalled();
    expect(mockPublishForm).not.toHaveBeenCalled();
  });

  it("closes without asking after a save, and asks once there are changes", async () => {
    storeSavesAs(1);
    const { screen, router } = await openPage();
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");
    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect
      .element(screen.getByText("Draft version 1 saved."))
      .toBeVisible();

    await jsonBody(screen).fill(`${EXAMPLE_TEXT}\n`);
    await expect
      .element(screen.getByText(/Draft version 1 saved · unsaved changes/))
      .toBeVisible();
    await screen.getByRole("link", { name: "← Form management" }).click();

    await expect
      .element(
        screen.getByRole("heading", {
          name: "Discard your changes since the last save? The saved draft is kept.",
        }),
      )
      .toHaveFocus();
    expect(router.state.location.pathname).not.toBe(paths.adminFormManagement);

    await screen.getByRole("button", { name: "Keep editing" }).click();
    await jsonBody(screen).fill(EXAMPLE_TEXT);
    await screen.getByRole("button", { name: "Close" }).click();

    await expect
      .element(screen.getByRole("heading", { name: "Form management list" }))
      .toBeVisible();
  });

  it("checks the form again with a read made after the refusal", async () => {
    const queryClient = newQueryClient();
    mockListForms
      .mockResolvedValueOnce([BUS_PASS_PUBLISHED])
      .mockResolvedValueOnce([BUS_PASS_PUBLISHED])
      .mockResolvedValue([BUS_PASS_WITH_DRAFT]);
    mockGetFormVersion.mockResolvedValue(BUS_PASS_V8);
    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json?confirm=new-9",
      queryClient,
    );
    await pasteValid(screen, BUS_PASS_TEXT, "First name");
    const save = deferred<FormSpecPayload>();
    mockSaveDraft.mockReturnValueOnce(save.promise);

    await screen.getByRole("button", { name: "Save draft" }).click();
    // A read of the list that ends while the save is still in flight.
    await queryClient.invalidateQueries({ queryKey: ["admin-forms"] });
    const reread = deferred<FormSummary[]>();
    mockListForms.mockReturnValueOnce(reread.promise);
    save.reject(taken('Version 9 of "bc-bus-pass" is an unpublished draft.'));

    const checking = screen.getByText("Checking…", { exact: true });
    await expect.element(checking).toBeVisible();
    await screen.getByRole("button", { name: "Save draft" }).click();
    await expect.element(checking).toHaveFocus();

    reread.resolve([BUS_PASS_WITH_DRAFT]);
    await expect
      .element(
        screen.getByRole("heading", {
          name: 'Replace the unpublished draft of "BC Bus Pass"?',
        }),
      )
      .toHaveFocus();
    expect(mockSaveDraft).toHaveBeenCalledTimes(1);
  });

  it("keeps the JSON in view while a new comparison version loads", async () => {
    const busPassV9: FormSummary = {
      ...BUS_PASS_PUBLISHED,
      versions: [
        ...BUS_PASS_PUBLISHED.versions,
        { version: 9, isPublished: true },
      ],
    };
    mockListForms
      .mockResolvedValueOnce([BUS_PASS_PUBLISHED])
      .mockResolvedValue([busPassV9]);
    let version9Fails = true;
    mockGetFormVersion.mockImplementation(async (_formSpecId, version) => {
      if (version === 9 && version9Fails) {
        throw new FormLoadError(404, "Version fetch failed (404)");
      }
      return { ...BUS_PASS_V8, version };
    });
    const { screen } = await renderPage(
      "/admin/form-management/bc-bus-pass/from-json?confirm=new-9",
    );
    mockSaveDraft.mockRejectedValueOnce(
      taken('Version 9 of "bc-bus-pass" already exists.'),
    );
    await pasteValid(screen, BUS_PASS_TEXT, "First name");
    await screen.getByRole("button", { name: "Save draft" }).click();

    await screen.getByRole("button", { name: "Create version 10" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Could not load version 9 of this form");
    await expect.element(jsonBody(screen)).toHaveValue(BUS_PASS_TEXT);
    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(/Loading version 9 to compare with\./);

    version9Fails = false;
    await screen.getByRole("button", { name: "Try again" }).click();

    await expect
      .element(jsonBody(screen))
      .toHaveAccessibleDescription(/Valid\. 2 questions\./);
    expect(mockGetFormVersion).toHaveBeenLastCalledWith("bc-bus-pass", 9);
  });

  it("says a save is still running when leaving during it", async () => {
    const { screen } = await openPage();
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");
    const save = deferred<FormSpecPayload>();
    mockSaveDraft.mockReturnValueOnce(save.promise);

    await screen.getByRole("button", { name: "Save draft" }).click();
    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect
      .element(
        screen.getByRole("heading", {
          name: "Leave before the save finishes? Your JSON may still be saved.",
        }),
      )
      .toHaveFocus();
    save.resolve({ ...BUS_PASS_V8, formSpecId: "zz-free-id", version: 1 });
    await expect
      .element(screen.getByText("Draft version 1 saved."))
      .toBeVisible();
  });

  it("shows a plain message for a refusal without reasons", async () => {
    const { screen } = await openPage();
    mockSaveDraft.mockRejectedValueOnce(new SpecRejectedError(502, []));
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");

    await screen.getByRole("button", { name: "Save draft" }).click();

    await expect
      .element(
        screen.getByText(
          "Could not save this form (error 502). Please try again.",
        ),
      )
      .toBeVisible();
    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
  });

  it("retries a failed publish with an ordinary save", async () => {
    storeSavesAs(1);
    mockPublishForm
      .mockRejectedValueOnce(taken("Publishing was refused."))
      .mockResolvedValue({ version: 1 });
    const { screen } = await openPage();
    await pasteValid(screen, EXAMPLE_TEXT, "Your full name");

    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(screen.getByText("Publishing was refused."))
      .toBeVisible();
    await expect
      .element(screen.getByText("Draft version 1 saved."))
      .toBeVisible();
    expect(screen.getByRole("textbox", { name: "Form ID" }).query()).toBeNull();

    await screen.getByRole("button", { name: "Publish" }).click();

    await expect
      .element(screen.getByText("Published version 1."))
      .toBeVisible();
    expect(savedInput(1)).not.toHaveProperty("newVersion");
    expect(mockPublishForm).toHaveBeenCalledTimes(2);
  });

  it("publishes after removed questions are confirmed from Publish", async () => {
    storeSavesAs(9);
    mockPublishForm.mockResolvedValue({ version: 9 });
    const { screen } = await openPage(BUS_PASS);
    await pasteValid(
      screen,
      JSON.stringify(withoutComponent(BUS_PASS_V8.spec, "email"), null, 2),
      "First name",
    );

    await screen.getByRole("button", { name: "Publish" }).click();
    await screen.getByRole("button", { name: "Save anyway" }).click();

    await expect
      .element(screen.getByText("Published version 9."))
      .toBeVisible();
    expect(mockPublishForm).toHaveBeenCalledTimes(1);
  });

  it("asks the browser to confirm a reload only with unsaved JSON", async () => {
    const { screen } = await openPage();
    const reload = () => {
      const event = new Event("beforeunload", { cancelable: true });
      window.dispatchEvent(event);
      return event.defaultPrevented;
    };
    expect(reload()).toBe(false);

    await jsonBody(screen).fill("{");

    await vi.waitFor(() => expect(reload()).toBe(true));
  });

  // The browser's own history: after Back, and after the router's undo of it,
  // the browser can restore a scroll position once the question has appeared.
  it("asks on the browser's Back button, with the question in view", async () => {
    const { innerWidth, innerHeight } = window;
    const startUrl = `${location.pathname}${location.search}`;
    await page.viewport(800, 500);
    const startState: unknown = history.state;
    mockListForms.mockResolvedValue([]);
    history.pushState(null, "", paths.adminFormManagement);
    // A list long enough to be left scrolled part of the way down.
    const router = createBrowserRouter([
      routes[0],
      {
        path: paths.adminFormManagement,
        element: (
          <h1 style={{ marginBottom: "200vh" }}>Form management list</h1>
        ),
      },
    ]);
    let pops = 0;
    const countPop = () => {
      pops += 1;
    };
    window.addEventListener("popstate", countPop);
    try {
      const screen = await render(
        <QueryClientProvider client={newQueryClient()}>
          <RouterProvider router={router} />
        </QueryClientProvider>,
      );
      window.scrollTo(0, 300);
      await router.navigate(
        "/admin/form-management/zz-free-id/from-json?title=Check",
      );
      await jsonBody(screen).fill("{");
      await expect
        .element(screen.getByRole("heading", { name: "There is a problem" }))
        .toBeVisible();
      window.scrollTo(0, 0);

      // Back, then the router's undo of it, then two frames.
      async function back() {
        const before = pops;
        history.back();
        await vi.waitFor(() => expect(pops).toBe(before + 2));
        await new Promise((done) => requestAnimationFrame(done));
        await new Promise((done) => requestAnimationFrame(done));
      }
      // Measured once, so a late jump out of view fails it.
      const name = "Discard this JSON? Nothing has been saved.";
      async function expectQuestionInView() {
        await expect
          .element(screen.getByRole("heading", { name }))
          .toHaveFocus();
        // The whole question, buttons included, within a pixel of rounding.
        const box = screen
          .getByRole("region", { name })
          .element()
          .getBoundingClientRect();
        expect(box.top).toBeGreaterThanOrEqual(0);
        expect(box.bottom).toBeLessThanOrEqual(window.innerHeight + 1);
        expect(location.pathname).toBe(
          "/admin/form-management/zz-free-id/from-json",
        );
      }

      await back();
      await expectQuestionInView();

      // Back again, while the question is shown.
      await back();
      await expectQuestionInView();
    } finally {
      window.removeEventListener("popstate", countPop);
      router.dispose();
      history.replaceState(startState, "", startUrl);
      await page.viewport(innerWidth, innerHeight);
    }
  });
});
