import {
  QueryClient,
  QueryClientProvider,
  useMutation,
} from "@tanstack/react-query";
import type { FormType } from "@formio/react/lib/components/Form";
import { afterEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";

import { ApplicationRequestError } from "../api/applications";
import { SubmissionRejectedError } from "../api/forms";
import DraftApplicationForm, {
  type DraftAnswersInput,
  type DraftApplication,
  type DraftFormMessages,
} from "./DraftApplicationForm";

// The shared draft form on its own, apart from either app's hooks: the
// mutations are real react-query ones over mock functions, so what is checked
// is what the form sends, what it shows for each answer, and that its wording
// is the caller's. The apps' own suites (ApplicationForm, WorkerOverrideForm)
// cover it again against their API stubs.

const NOT_EDITABLE = "INTAKE.APPLICATION.NOT_EDITABLE";

const messages: DraftFormMessages = {
  conflict: "Test: someone else saved first.",
  notEditable: "Test: this cannot change now.",
  returned: "Test: more information was asked for.",
  saveFailed: "Test: the save failed:",
  savedHint: "Test: come back later.",
  saveLabel: "Keep for later",
};

const spec = {
  display: "form",
  components: [
    {
      type: "textfield",
      key: "firstName",
      label: "First name",
      input: true,
      validate: { required: true },
    },
    {
      type: "textfield",
      key: "lastName",
      label: "Last name",
      input: true,
      validate: { required: true },
    },
    {
      type: "button",
      key: "submit",
      action: "submit",
      label: "Submit",
      input: true,
    },
  ],
} as unknown as FormType;

type Saved = { updatedAt?: string | null };
type Write = (input: DraftAnswersInput) => Promise<Saved>;

function draft(overrides: Partial<DraftApplication> = {}): DraftApplication {
  return { version: 3, status: "DRAFT", answers: {}, ...overrides };
}

function Harness({
  application,
  saveFn,
  submitFn,
  onSubmitted,
}: Readonly<{
  application: DraftApplication;
  saveFn: Write;
  submitFn: Write;
  onSubmitted: () => void;
}>) {
  const save = useMutation({ mutationFn: saveFn });
  const submit = useMutation({ mutationFn: submitFn });
  return (
    <DraftApplicationForm
      application={application}
      spec={spec}
      save={save}
      submit={submit}
      onSubmitted={onSubmitted}
      notEditableKeyword={NOT_EDITABLE}
      messages={messages}
    />
  );
}

async function setup({
  application = draft(),
  saveFn = vi.fn<Write>().mockResolvedValue({
    updatedAt: "2026-11-03T10:05:00Z",
  }),
  submitFn = vi.fn<Write>().mockResolvedValue({}),
}: {
  application?: DraftApplication;
  saveFn?: ReturnType<typeof vi.fn<Write>>;
  submitFn?: ReturnType<typeof vi.fn<Write>>;
} = {}) {
  const onSubmitted = vi.fn();
  const queryClient = new QueryClient({
    defaultOptions: { mutations: { retry: false } },
  });
  const tree = (app: DraftApplication) => (
    <QueryClientProvider client={queryClient}>
      <Harness
        application={app}
        saveFn={saveFn}
        submitFn={submitFn}
        onSubmitted={onSubmitted}
      />
    </QueryClientProvider>
  );
  return {
    saveFn,
    submitFn,
    onSubmitted,
    tree,
    screen: await render(tree(application)),
  };
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("DraftApplicationForm", () => {
  it("shows the answers as loaded, with the caller's Save label", async () => {
    const { screen } = await setup({
      application: draft({ answers: { firstName: "Ada" } }),
    });

    await expect
      .element(screen.getByRole("textbox", { name: /First name/ }))
      .toHaveValue("Ada");
    await expect
      .element(screen.getByRole("button", { name: "Keep for later" }))
      .toBeEnabled();
  });

  it("saves a half-filled draft with the version it was read at", async () => {
    const { screen, saveFn, submitFn } = await setup();

    await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
    await screen.getByRole("button", { name: "Keep for later" }).click();

    await expect
      .element(screen.getByText(/^Saved at .+\. Test: come back later\.$/))
      .toBeVisible();
    expect(saveFn).toHaveBeenCalledOnce();
    expect(saveFn.mock.calls[0][0]).toEqual({
      version: 3,
      answers: expect.objectContaining({ firstName: "Ada" }),
    });
    expect(submitFn).not.toHaveBeenCalled();
  });

  it("holds Save while a save is in flight", async () => {
    const saveFn = vi.fn<Write>(() => new Promise<Saved>(() => {}));
    const { screen } = await setup({ saveFn });

    await screen.getByRole("button", { name: "Keep for later" }).click();

    await expect
      .element(screen.getByRole("button", { name: "Keep for later" }))
      .toBeDisabled();
    expect(saveFn).toHaveBeenCalledOnce();
  });

  it("submits the answers with the version and reports the submit", async () => {
    const { screen, submitFn, onSubmitted } = await setup();

    await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
    await screen.getByRole("textbox", { name: /Last name/ }).fill("Lovelace");
    await screen.getByRole("button", { name: "Submit" }).click();

    await vi.waitFor(() => expect(onSubmitted).toHaveBeenCalledOnce());
    expect(submitFn).toHaveBeenCalledOnce();
    expect(submitFn.mock.calls[0][0]).toEqual({
      version: 3,
      answers: expect.objectContaining({
        firstName: "Ada",
        lastName: "Lovelace",
      }),
    });
  });

  it("does not send a submit the form itself refuses", async () => {
    const { screen, submitFn } = await setup();

    await screen.getByRole("button", { name: "Submit" }).click();

    // Form.io's own checks block it; the summary lists what is missing.
    await expect.element(screen.getByRole("alert")).toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: /First name/ }).first())
      .toBeVisible();
    expect(submitFn).not.toHaveBeenCalled();
  });

  it("pins the API's field errors on the form, and lets an edit clear them", async () => {
    const submitFn = vi
      .fn<Write>()
      .mockRejectedValueOnce(
        new SubmissionRejectedError(422, [
          {
            field: "lastName",
            keyword: "FORM.FIELD.REQUIRED",
            message: "Enter a real last name.",
          },
        ]),
      )
      .mockResolvedValueOnce({});
    const { screen, onSubmitted } = await setup({ submitFn });

    await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
    await screen.getByRole("textbox", { name: /Last name/ }).fill("L");
    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(screen.getByText("Enter a real last name.").first())
      .toBeVisible();
    expect(onSubmitted).not.toHaveBeenCalled();

    // Without the edit clearing the server error, Form.io would still count
    // the field invalid and refuse this submit client-side.
    await screen.getByRole("textbox", { name: /Last name/ }).fill("Lovelace");
    await screen.getByRole("button", { name: "Submit" }).click();

    await vi.waitFor(() => expect(onSubmitted).toHaveBeenCalledOnce());
    expect(submitFn).toHaveBeenCalledTimes(2);
  });

  it("says so when another tab or worker saved first", async () => {
    const saveFn = vi
      .fn<Write>()
      .mockRejectedValue(
        new ApplicationRequestError(409, "INTAKE.APPLICATION.CONFLICT", 4),
      );
    const { screen } = await setup({ saveFn });

    await screen.getByRole("button", { name: "Keep for later" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent(messages.conflict);
  });

  it("says the application cannot change when the API says so", async () => {
    const submitFn = vi
      .fn<Write>()
      .mockRejectedValue(new ApplicationRequestError(409, NOT_EDITABLE, 4));
    const { screen, onSubmitted } = await setup({ submitFn });

    await screen.getByRole("textbox", { name: /First name/ }).fill("Ada");
    await screen.getByRole("textbox", { name: /Last name/ }).fill("Lovelace");
    await screen.getByRole("button", { name: "Submit" }).click();

    // A conflict replaces the error summary rather than joining it.
    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent(messages.notEditable);
    expect(onSubmitted).not.toHaveBeenCalled();
  });

  it("names a save that failed for another reason", async () => {
    const saveFn = vi
      .fn<Write>()
      .mockRejectedValue(
        new ApplicationRequestError(500, undefined, undefined, "Server down."),
      );
    const { screen } = await setup({ saveFn });

    await screen.getByRole("button", { name: "Keep for later" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Test: the save failed: Server down.");
    await expect.element(screen.getByText(/^Saved/)).not.toBeInTheDocument();
  });

  it("asks for an update when the application was returned", async () => {
    const { screen } = await setup({
      application: draft({ status: "RETURNED" }),
    });

    await expect.element(screen.getByText(messages.returned)).toBeVisible();
  });

  it("shows no returned notice on a plain draft", async () => {
    const { screen } = await setup();

    await expect
      .element(screen.getByRole("button", { name: "Keep for later" }))
      .toBeVisible();
    await expect
      .element(screen.getByText(messages.returned))
      .not.toBeInTheDocument();
  });

  it("keeps what was typed when the application refetches", async () => {
    const { screen, tree } = await setup({
      application: draft({ answers: { firstName: "Ada" } }),
    });

    await screen.getByRole("textbox", { name: /First name/ }).fill("Grace");
    // A refetch after a save brings new answers and a new version; the form
    // must not push them back over what is on screen.
    await screen.rerender(
      tree(draft({ version: 4, answers: { firstName: "Ada" } })),
    );

    await expect
      .element(screen.getByRole("textbox", { name: /First name/ }))
      .toHaveValue("Grace");
  });

  it("sends the newest version after a refetch", async () => {
    const { screen, tree, saveFn } = await setup();

    await screen.rerender(tree(draft({ version: 7 })));
    await screen.getByRole("button", { name: "Keep for later" }).click();

    await vi.waitFor(() => expect(saveFn).toHaveBeenCalledOnce());
    expect(saveFn.mock.calls[0][0].version).toBe(7);
  });
});
