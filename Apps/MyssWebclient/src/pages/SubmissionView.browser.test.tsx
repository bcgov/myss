import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import SubmissionView from "@/pages/SubmissionView";
import type { FormSubmissionPayload } from "@/hooks/usePocForm";

// A stored submission renders read-only against the spec version the API
// returns with it, and still renders when that spec is missing.

const SUBMISSION_ID = "11111111-2222-3333-4444-555555555555";

// A v1 spec with the old income label and the conditional spouse field,
// so it differs from the current form.
const archivedV1Submission: FormSubmissionPayload = {
  id: SUBMISSION_ID,
  formSpecId: "poc-test-form",
  formSpecVersion: 1,
  submittedAt: "2026-07-29T18:17:41Z",
  answers: {
    firstName: "Ada",
    lastName: "Lovelace",
    relationship: "couple",
    spouseName: "William King",
    monthlyIncome: 1200,
    declaration: true,
  },
  spec: {
    formSpecId: "poc-test-form",
    version: 1,
    title: "POC test form",
    spec: {
      display: "form",
      components: [
        {
          type: "textfield",
          key: "firstName",
          label: "First name",
          input: true,
        },
        {
          type: "textfield",
          key: "spouseName",
          label: "Spouse name",
          input: true,
          conditional: { show: true, when: "relationship", eq: "couple" },
        },
        {
          type: "number",
          key: "monthlyIncome",
          label: "Monthly income ($)",
          input: true,
        },
      ],
    },
  },
};

function stubSubmissionFetch(payload: FormSubmissionPayload) {
  vi.spyOn(window, "fetch").mockImplementation(async (input) => {
    const url = String(input);
    if (url.includes(`/v1/forms/submissions/${SUBMISSION_ID}`)) {
      return new Response(JSON.stringify({ payload }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    throw new Error(`Unexpected fetch in test: ${url}`);
  });
}

function renderView() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter
        initialEntries={[`/techdemos/forms/submissions/${SUBMISSION_ID}`]}
      >
        <Routes>
          <Route
            path="/techdemos/forms/submissions/:id"
            element={<SubmissionView />}
          />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("renders the archived spec version, read-only, with the stored answers", async () => {
  stubSubmissionFetch(archivedV1Submission);
  const screen = await renderView();

  await expect.element(screen.getByText("poc-test-form v1")).toBeVisible();

  // The v1-only label renders, and the conditional spouse field resolves
  // from the stored answers.
  await expect.element(screen.getByText("Monthly income ($)")).toBeVisible();
  const spouse = screen.getByRole("textbox", { name: "Spouse name" });
  await expect.element(spouse).toBeVisible();

  // Read-only: values shown, editing disabled.
  const firstName = screen.getByRole("textbox", { name: "First name" });
  await expect.element(firstName).toHaveValue("Ada");
  await expect.element(firstName).toBeDisabled();
  await expect.element(spouse).toHaveValue("William King");
  await expect.element(spouse).toBeDisabled();
});

test("falls back to a message when the archived spec version is gone", async () => {
  stubSubmissionFetch({ ...archivedV1Submission, spec: null });
  const screen = await renderView();

  await expect
    .element(screen.getByText(/archived spec version v1 is no longer/))
    .toBeVisible();

  // The submission metadata still renders.
  await expect.element(screen.getByText(SUBMISSION_ID)).toBeVisible();
});
