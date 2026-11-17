import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import FormMessagePage from "./FormMessagePage";
import {
  FormMessageLoadError,
  FormMessageRejectedError,
  getFormMessage,
  saveFormMessage,
  type FormMessageDetail,
  type FormMessageEntry,
} from "@/api/formMessages";
import { listForms } from "@/api/forms";

// One form message's page against a stubbed admin API: the calls are mocked;
// the preview, history, editor, rules and messages are the real code.

vi.mock("@/api/formMessages", async (importActual) => ({
  ...(await importActual<typeof import("@/api/formMessages")>()),
  getFormMessage: vi.fn(),
  saveFormMessage: vi.fn(),
}));

vi.mock("@/api/forms", async (importActual) => ({
  ...(await importActual<typeof import("@/api/forms")>()),
  listForms: vi.fn(),
}));

const mockGetFormMessage = vi.mocked(getFormMessage);
const mockSaveFormMessage = vi.mocked(saveFormMessage);
const mockListForms = vi.mocked(listForms);

const definition = {
  formSpecId: "bc-bus-pass",
  messageKey: "confirmation",
  name: "Confirmation message",
  description: "Shown after a request is submitted successfully.",
  placeholders: [
    {
      name: "referenceNumber",
      description: "the applicant's reference number",
      sample: "1-0123456789",
      required: true,
    },
  ],
};

const older: FormMessageEntry = {
  id: "entry-1",
  heading: "Old heading",
  title: "Old title",
  body: "Old text {referenceNumber}",
  changedBy: "AADAMS",
  changedAt: "2026-06-30T16:00:00Z",
};

const live: FormMessageEntry = {
  id: "entry-2",
  heading: "Your request is in",
  title: "We have it",
  body: "Your reference number is **{referenceNumber}**.",
  changedBy: "JSMITH",
  changedAt: "2026-07-03T17:42:00Z",
};

const withEntries: FormMessageDetail = {
  definition,
  current: live,
  history: [live, older],
};

const withNone: FormMessageDetail = { definition, current: null, history: [] };

function renderPage(messageKey = "confirmation") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter
        initialEntries={[`/form-management/bc-bus-pass/messages/${messageKey}`]}
      >
        <Routes>
          <Route
            path="/form-management/:formSpecId/messages/:messageKey"
            element={<FormMessagePage />}
          />
          <Route
            path="/form-management/:formSpecId/messages"
            element={<p>Form messages opened</p>}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

type Screen = Awaited<ReturnType<typeof renderPage>>;

function headingField(screen: Screen) {
  return screen.getByRole("textbox", { name: /^Page heading/ });
}

function titleField(screen: Screen) {
  return screen.getByRole("textbox", { name: /^Message title/ });
}

function bodyField(screen: Screen) {
  return screen.getByRole("textbox", { name: /^Message(?! title)/ });
}

async function startEditing(screen: Screen) {
  await screen.getByRole("button", { name: "Edit" }).click();
  await expect.element(headingField(screen)).toHaveFocus();
}

async function expectRefused(screen: Screen, message: string) {
  const summary = screen.getByRole("alert");
  await expect.element(summary).toHaveTextContent(message);
  await expect
    .element(screen.getByRole("heading", { name: "There is a problem" }))
    .toHaveFocus();
}

beforeEach(() => {
  mockListForms.mockResolvedValue([
    {
      formSpecId: "bc-bus-pass",
      title: "BC Bus Pass",
      versions: [{ version: 8, isPublished: true }],
    },
  ]);
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("FormMessagePage: read view", () => {
  it("shows the live message with the sample value, its last change in BC time, and the history", async () => {
    mockGetFormMessage.mockResolvedValue(withEntries);

    const screen = await renderPage();

    await expect
      .element(screen.getByRole("heading", { level: 1, name: "Confirmation message" }))
      .toBeVisible();
    await expect.element(screen.getByText("BC Bus Pass")).toBeVisible();
    await expect
      .element(screen.getByText("Shown after a request is submitted successfully."))
      .toBeVisible();
    await expect
      .element(screen.getByText("Last changed on 3 Jul 2026 at 10:42 by JSMITH"))
      .toBeVisible();
    const preview = screen.getByRole("figure");
    await expect.element(preview.getByText("Your request is in")).toBeVisible();
    await expect
      .element(preview.getByRole("note", { name: "We have it" }))
      .toBeVisible();
    expect(preview.element().querySelector("strong:not(figcaption strong)")?.textContent).toBe(
      "1-0123456789",
    );
    await expect
      .element(preview.getByText("{referenceNumber} shows the sample 1-0123456789"))
      .toBeVisible();

    const history = screen.getByRole("region", { name: "Change history" });
    const times = history
      .getByRole("row")
      .elements()
      .slice(1)
      .map((row) => row.querySelector("td")?.textContent);
    expect(times).toEqual(["3 Jul 2026 at 10:42", "30 Jun 2026 at 09:00"]);
    await expect.element(history.getByRole("row").nth(1)).toHaveTextContent("JSMITH Live");

    await history.getByRole("button", { name: "Show text" }).nth(1).click();
    await expect
      .element(history.getByRole("button", { name: "Hide text" }))
      .toHaveAttribute("aria-expanded", "true");
    await expect.element(history.getByText("Old text {referenceNumber}")).toBeVisible();
    await expect.element(history.getByText("Old title")).toBeVisible();
  });

  it("shows the built-in message when none is saved", async () => {
    mockGetFormMessage.mockResolvedValue(withNone);

    const screen = await renderPage();

    await expect
      .element(screen.getByText("Not changed yet. Applicants see the built-in message."))
      .toBeVisible();
    const preview = screen.getByRole("figure");
    await expect.element(preview.getByText("Bus Pass Request is complete")).toBeVisible();
    await expect
      .element(preview.getByRole("note", { name: "Request submitted" }))
      .toBeVisible();
    await expect
      .element(preview.getByText(/Your reference number is 1-0123456789\. Keep it/))
      .toBeVisible();
    await expect.element(screen.getByText("No changes yet.")).toBeVisible();
  });

  it("links back to the form's messages", async () => {
    mockGetFormMessage.mockResolvedValue(withNone);

    const screen = await renderPage();

    const back = screen.getByRole("link", { name: "← Form messages" });
    await expect.element(back).toHaveAttribute("href", "/form-management/bc-bus-pass/messages");
    await back.click();
    await expect.element(screen.getByText("Form messages opened")).toBeVisible();
  });

  it("says when the form has no message by that name", async () => {
    mockGetFormMessage.mockRejectedValue(new FormMessageLoadError(404, "Not found"));

    const screen = await renderPage("intro");

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("This form has no message called “intro”.");
    await expect
      .element(screen.getByRole("link", { name: "Back to Form messages" }))
      .toHaveAttribute("href", "/form-management/bc-bus-pass/messages");
  });

  it("explains a 403 in place of the page", async () => {
    mockGetFormMessage.mockRejectedValue(new FormMessageLoadError(403, "Forbidden"));

    const screen = await renderPage();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Your account is not allowed to open the form messages (error 403).");
    await expect.element(screen.getByRole("button", { name: "Edit" })).not.toBeInTheDocument();
  });
});

describe("FormMessagePage: editing", () => {
  it("starts from the live message, previews the typing, and saves and publishes it", async () => {
    mockGetFormMessage.mockResolvedValue(withEntries);
    const saved: FormMessageEntry = {
      ...live,
      id: "entry-3",
      heading: "Changed heading",
      changedBy: "MADMIN",
      changedAt: "2026-07-04T18:05:00Z",
    };
    mockSaveFormMessage.mockResolvedValue({
      definition,
      current: saved,
      history: [saved, live, older],
    });
    const screen = await renderPage();

    await startEditing(screen);
    await expect.element(headingField(screen)).toHaveValue(live.heading);
    await expect.element(bodyField(screen)).toHaveValue(live.body);
    await headingField(screen).fill("Changed heading");
    await expect
      .element(screen.getByRole("figure").getByText("Changed heading"))
      .toBeVisible();
    await screen.getByRole("button", { name: "Save" }).click();

    await expect
      .element(screen.getByText("Form message saved and published."))
      .toBeVisible();
    expect(mockSaveFormMessage).toHaveBeenCalledWith("bc-bus-pass", "confirmation", {
      basedOn: "entry-2",
      heading: "Changed heading",
      title: live.title,
      body: live.body,
    });
    await expect.element(screen.getByRole("button", { name: "Edit" })).toHaveFocus();
    await expect
      .element(screen.getByText("Last changed on 4 Jul 2026 at 11:05 by MADMIN"))
      .toBeVisible();
    const firstRow = screen
      .getByRole("region", { name: "Change history" })
      .getByRole("row")
      .nth(1);
    await expect.element(firstRow).toHaveTextContent("4 Jul 2026 at 11:05");
  });

  it("starts from the built-in message when none is saved, and posts no entry to build on", async () => {
    mockGetFormMessage.mockResolvedValue(withNone);
    mockSaveFormMessage.mockResolvedValue(withEntries);
    const screen = await renderPage();

    await startEditing(screen);
    await expect.element(headingField(screen)).toHaveValue("Bus Pass Request is complete");
    await screen.getByRole("button", { name: "Save" }).click();

    await expect
      .element(screen.getByText("Form message saved and published."))
      .toBeVisible();
    expect(mockSaveFormMessage.mock.calls[0][2].basedOn).toBeNull();
  });

  it("reads the message again after a conflict, so Cancel shows it and the next save builds on it", async () => {
    const newer: FormMessageEntry = {
      ...live,
      id: "entry-3",
      title: "Request received",
      changedBy: "BBROWN",
      changedAt: "2026-07-04T18:05:00Z",
    };
    const afterNewer: FormMessageDetail = {
      definition,
      current: newer,
      history: [newer, live, older],
    };
    mockGetFormMessage.mockResolvedValueOnce(withEntries).mockResolvedValue(afterNewer);
    mockSaveFormMessage
      .mockRejectedValueOnce(
        new FormMessageRejectedError(409, [
          { field: "basedOn", keyword: "FORM_MESSAGE.CHANGED", message: "Changed." },
        ]),
      )
      .mockResolvedValue(afterNewer);
    const screen = await renderPage();

    await startEditing(screen);
    await screen.getByRole("button", { name: "Save" }).click();
    await expectRefused(screen, "This message changed after you started editing.");
    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect
      .element(screen.getByText("Last changed on 4 Jul 2026 at 11:05 by BBROWN"))
      .toBeVisible();
    await startEditing(screen);
    await screen.getByRole("button", { name: "Save" }).click();
    await expect
      .element(screen.getByText("Form message saved and published."))
      .toBeVisible();
    expect(mockSaveFormMessage.mock.calls[1][2].basedOn).toBe("entry-3");
  });

  it("puts the read view back on Cancel, with focus on Edit", async () => {
    mockGetFormMessage.mockResolvedValue(withEntries);
    const screen = await renderPage();

    await startEditing(screen);
    await headingField(screen).fill("Not kept");
    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect.element(screen.getByRole("button", { name: "Edit" })).toHaveFocus();
    await expect.element(headingField(screen)).not.toBeInTheDocument();
    await expect
      .element(screen.getByRole("figure").getByText("Your request is in"))
      .toBeVisible();
    expect(mockSaveFormMessage).not.toHaveBeenCalled();
  });

  it("keeps Formatting help closed until opened, then shows the marks, the placeholder and an example", async () => {
    mockGetFormMessage.mockResolvedValue(withEntries);
    const screen = await renderPage();

    await startEditing(screen);
    const toggle = screen.getByRole("button", { name: "Formatting help" });
    await expect.element(toggle).toHaveAttribute("aria-expanded", "false");
    await expect.element(screen.getByText("**bold text**")).not.toBeVisible();

    await toggle.click();

    await expect.element(toggle).toHaveAttribute("aria-expanded", "true");
    await expect.element(screen.getByText("**bold text**")).toBeVisible();
    await expect.element(screen.getByText("[link text](https://…)")).toBeVisible();
    await expect.element(screen.getByText("{referenceNumber}", { exact: true })).toBeVisible();
    await expect
      .element(screen.getByText("The applicant's reference number (required)"))
      .toBeVisible();
    await expect.element(screen.getByText(/\[Visit our website\]/)).toBeVisible();
  });
});

describe("FormMessagePage: refusals", () => {
  beforeEach(() => {
    mockGetFormMessage.mockResolvedValue(withEntries);
  });

  it("refuses a blank heading and message, by field, and sends nothing", async () => {
    const screen = await renderPage();
    await startEditing(screen);

    await headingField(screen).fill("");
    await bodyField(screen).fill("   ");
    await screen.getByRole("button", { name: "Save" }).click();

    await expectRefused(screen, "Enter a page heading.");
    await expect.element(screen.getByRole("alert")).toHaveTextContent("Enter the message.");
    await expect.element(headingField(screen)).toHaveAttribute("aria-invalid", "true");
    await expect.element(bodyField(screen)).toHaveAttribute("aria-invalid", "true");
    await expect.element(titleField(screen)).not.toHaveAttribute("aria-invalid");
    expect(mockSaveFormMessage).not.toHaveBeenCalled();

    await screen.getByRole("button", { name: "Enter the message." }).click();
    await expect.element(bodyField(screen)).toHaveFocus();
  });

  it("drops a field's refusals as soon as it is edited, and the box once none are left", async () => {
    const screen = await renderPage();
    await startEditing(screen);
    await headingField(screen).fill("");
    await bodyField(screen).fill("");
    await screen.getByRole("button", { name: "Save" }).click();
    await expectRefused(screen, "Enter a page heading.");

    await headingField(screen).fill("A new heading");

    await expect.element(headingField(screen)).toHaveFocus();
    const summary = screen.getByRole("alert");
    await expect.element(summary).not.toHaveTextContent("Enter a page heading.");
    await expect.element(summary).toHaveTextContent("Enter the message.");
    await expect.element(headingField(screen)).not.toHaveAttribute("aria-invalid");
    await expect.element(bodyField(screen)).toHaveAttribute("aria-invalid", "true");

    await bodyField(screen).fill("Your number is {referenceNumber}.");

    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .not.toBeInTheDocument();
    await expect.element(bodyField(screen)).not.toHaveAttribute("aria-invalid");
    expect(mockSaveFormMessage).not.toHaveBeenCalled();
  });

  it("keeps a refusal that is not about one field until the next save", async () => {
    mockSaveFormMessage.mockRejectedValue(new FormMessageRejectedError(502, []));
    const screen = await renderPage();
    await startEditing(screen);
    await screen.getByRole("button", { name: "Save" }).click();
    await expectRefused(screen, "The save could not be confirmed (error 502).");

    await headingField(screen).fill("Another heading");

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("The save could not be confirmed (error 502).");
  });

  it("lets an over-long message be typed, counts it, and refuses it", async () => {
    const screen = await renderPage();
    await startEditing(screen);

    await bodyField(screen).fill(`{referenceNumber}${"x".repeat(984)}`);
    await expect.element(screen.getByText("1001/1000")).toBeVisible();
    await screen.getByRole("button", { name: "Save" }).click();

    await expectRefused(
      screen,
      "The message must be 1,000 characters or fewer. It has 1,001.",
    );
    expect(mockSaveFormMessage).not.toHaveBeenCalled();
  });

  it("refuses a message without the reference number", async () => {
    const screen = await renderPage();
    await startEditing(screen);

    await bodyField(screen).fill("Thank you.");
    await screen.getByRole("button", { name: "Save" }).click();

    await expectRefused(
      screen,
      "Add {referenceNumber} where the applicant's reference number should appear.",
    );
    expect(mockSaveFormMessage).not.toHaveBeenCalled();
  });

  it("refuses a link that is not https, email or phone", async () => {
    const screen = await renderPage();
    await startEditing(screen);

    await bodyField(screen).fill("{referenceNumber} [Open](javascript:alert(1))");
    await screen.getByRole("button", { name: "Save" }).click();

    await expectRefused(
      screen,
      "The link “Open” must start with https://, mailto: or tel:, with no spaces.",
    );
    expect(mockSaveFormMessage).not.toHaveBeenCalled();
  });

  it("shows a rule only the server applies on its field", async () => {
    mockSaveFormMessage.mockRejectedValue(
      new FormMessageRejectedError(422, [
        { field: "title", keyword: "FORM_MESSAGE.TEXT.OTHER", message: "The server refused the title." },
      ]),
    );
    const screen = await renderPage();
    await startEditing(screen);

    await screen.getByRole("button", { name: "Save" }).click();

    await expectRefused(screen, "The server refused the title.");
    await expect.element(titleField(screen)).toHaveAttribute("aria-invalid", "true");
  });

  it.each([
    [
      409,
      [{ field: "basedOn", keyword: "FORM_MESSAGE.CHANGED", message: "Changed." }],
      "This message changed after you started editing. Your changes are still here. Cancel to see the current message, then make your changes again.",
    ],
    [401, [], "Could not save the message: you are no longer signed in (error 401)."],
    [403, [], "Could not save the message: your account is not allowed to change it (error 403)."],
    [502, [], "The save could not be confirmed (error 502). The message may not have been saved. Your changes are still here"],
  ])("explains a %i and keeps the edits", async (status, errors, message) => {
    mockSaveFormMessage.mockRejectedValue(new FormMessageRejectedError(status, errors));
    const screen = await renderPage();
    await startEditing(screen);

    await headingField(screen).fill("Kept heading");
    await screen.getByRole("button", { name: "Save" }).click();

    await expectRefused(screen, message);
    await expect.element(headingField(screen)).toHaveValue("Kept heading");
  });
});
