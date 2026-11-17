import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, describe, expect, it, vi } from "vitest";

import FormMessagesPage from "./FormMessagesPage";
import {
  FormMessageLoadError,
  listFormMessages,
  type FormMessageSummary,
} from "@/api/formMessages";
import { listForms, type FormSummary } from "@/api/forms";

// One form's messages against a stubbed admin API: the calls are mocked; the
// table, links and messages are the real code.

vi.mock("@/api/formMessages", async (importActual) => ({
  ...(await importActual<typeof import("@/api/formMessages")>()),
  listFormMessages: vi.fn(),
}));

vi.mock("@/api/forms", async (importActual) => ({
  ...(await importActual<typeof import("@/api/forms")>()),
  listForms: vi.fn(),
}));

const mockListFormMessages = vi.mocked(listFormMessages);
const mockListForms = vi.mocked(listForms);

const forms: FormSummary[] = [
  {
    formSpecId: "bc-bus-pass",
    title: "BC Bus Pass",
    versions: [{ version: 8, isPublished: true }],
  },
];

function summary(
  formSpecId: string,
  messageKey: string,
  name: string,
  change: Partial<Pick<FormMessageSummary, "lastChangedAt" | "lastChangedBy">> = {},
): FormMessageSummary {
  return {
    definition: {
      formSpecId,
      messageKey,
      name,
      description: `Where ${name} shows.`,
      placeholders: [],
    },
    lastChangedAt: change.lastChangedAt ?? null,
    lastChangedBy: change.lastChangedBy ?? null,
  };
}

function renderPage(formSpecId = "bc-bus-pass") {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`/form-management/${formSpecId}/messages`]}>
        <Routes>
          <Route
            path="/form-management/:formSpecId/messages"
            element={<FormMessagesPage />}
          />
          <Route path="/form-management" element={<p>Form management opened</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.clearAllMocks();
});

describe("FormMessagesPage", () => {
  it("lists the form's messages in order, leaving out another form's", async () => {
    mockListForms.mockResolvedValue(forms);
    mockListFormMessages.mockResolvedValue([
      summary("bc-bus-pass", "confirmation", "Confirmation message"),
      summary("other-form", "intro", "Other form's message"),
      summary("bc-bus-pass", "reminder", "Reminder message"),
    ]);

    const screen = await renderPage();

    await expect
      .element(screen.getByRole("heading", { level: 1, name: "Form messages" }))
      .toBeVisible();
    await expect.element(screen.getByText("BC Bus Pass")).toBeVisible();
    await expect
      .element(screen.getByRole("link", { name: "Confirmation message" }))
      .toBeVisible();
    const names = screen
      .getByRole("row")
      .elements()
      .slice(1)
      .map((row) => row.querySelector("td")?.textContent);
    expect(names).toEqual(["Confirmation message", "Reminder message"]);
    await expect
      .element(screen.getByText("Where Confirmation message shows."))
      .toBeVisible();
  });

  it("links each message to its editor and returns to Form management", async () => {
    mockListForms.mockResolvedValue(forms);
    mockListFormMessages.mockResolvedValue([
      summary("bc-bus-pass", "confirmation", "Confirmation message"),
    ]);

    const screen = await renderPage();

    await expect
      .element(screen.getByRole("link", { name: "Confirmation message" }))
      .toHaveAttribute("href", "/form-management/bc-bus-pass/messages/confirmation");
    const back = screen.getByRole("link", { name: "← Form management" });
    await expect.element(back).toHaveAttribute("href", "/form-management");
    await back.click();
    await expect.element(screen.getByText("Form management opened")).toBeVisible();
  });

  it("says when a message has not been changed, and when it last was, in BC time", async () => {
    mockListForms.mockResolvedValue(forms);
    mockListFormMessages.mockResolvedValue([
      summary("bc-bus-pass", "confirmation", "Confirmation message", {
        lastChangedAt: "2026-07-03T17:42:00Z",
        lastChangedBy: "JSMITH",
      }),
      summary("bc-bus-pass", "reminder", "Reminder message"),
    ]);

    const screen = await renderPage();

    await expect
      .element(screen.getByText("3 Jul 2026 at 10:42 by JSMITH"))
      .toBeVisible();
    await expect
      .element(
        screen.getByText("Not changed yet: applicants see the built-in message"),
      )
      .toBeVisible();
  });

  it("names the form by its ID when the forms list lacks it", async () => {
    mockListForms.mockResolvedValue([]);
    mockListFormMessages.mockResolvedValue([
      summary("bc-bus-pass", "confirmation", "Confirmation message"),
    ]);

    const screen = await renderPage();

    await expect
      .element(screen.getByRole("link", { name: "Confirmation message" }))
      .toBeVisible();
    await expect.element(screen.getByText("bc-bus-pass", { exact: true })).toBeVisible();
  });

  it("says when the form has no messages", async () => {
    mockListForms.mockResolvedValue(forms);
    mockListFormMessages.mockResolvedValue([
      summary("other-form", "intro", "Other form's message"),
    ]);

    const screen = await renderPage();

    await expect
      .element(screen.getByText("This form has no messages to edit."))
      .toBeVisible();
    await expect
      .element(screen.getByRole("link", { name: "Back to Form management" }))
      .toHaveAttribute("href", "/form-management");
    await expect.element(screen.getByRole("table")).not.toBeInTheDocument();
  });

  it.each([
    [403, "Your account is not allowed to open the form messages (error 403)."],
    [502, "Could not load the form messages (error 502). Reload the page to try again."],
  ])("explains a %i", async (status, message) => {
    mockListForms.mockResolvedValue(forms);
    mockListFormMessages.mockRejectedValue(
      new FormMessageLoadError(status, "Form messages fetch failed"),
    );

    const screen = await renderPage();

    await expect.element(screen.getByRole("alert")).toHaveTextContent(message);
  });
});
