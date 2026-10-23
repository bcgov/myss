import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes, useParams } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import type { ReviewApplicationSummaryPayload } from "@/api/review";
import { paths } from "@/routes/paths";
import ReviewQueue from "@/widgets/review/ReviewQueue";

// The worker's list: one row per submitted application, split into the
// queue and the rest, the request number as the link into it, and the status
// pill saying where the file is and who has it under review.

const FIRST_ID = "11111111-2222-3333-4444-555555555555";
const SECOND_ID = "22222222-3333-4444-5555-666666666666";

const queue: ReviewApplicationSummaryPayload[] = [
  {
    id: FIRST_ID,
    referenceNumber: "IA-11111111",
    status: "SUBMITTED",
    submittedAt: "2026-09-28T11:00:00Z",
    streamVersion: 1,
    inQueue: true,
  },
  {
    id: SECOND_ID,
    referenceNumber: "IA-22222222",
    status: "ACCEPTED",
    submittedAt: "2026-09-27T09:30:00Z",
    streamVersion: 3,
    inQueue: false,
  },
];

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function stubApi(answer: { status: number; body: unknown }) {
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url.endsWith("/v1/intake/review/applications") && method === "GET") {
      return json(answer.status, answer.body);
    }
    throw new Error(`Unexpected fetch in test: ${method} ${url}`);
  });
}

function ReviewLanding() {
  const { id } = useParams<{ id: string }>();
  return <p>Review landing {id}</p>;
}

function renderQueue() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[paths.applications]}>
        <Routes>
          <Route path={paths.applications} element={<ReviewQueue />} />
          <Route path={paths.application} element={<ReviewLanding />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("lists every submitted application with its request number, date and status", async () => {
  stubApi({ status: 200, body: { payload: queue } });
  const screen = await renderQueue();

  const queueTable = screen.getByRole("table", { name: "Queue" });
  await expect
    .element(queueTable.getByRole("columnheader", { name: "Request Number" }))
    .toBeVisible();
  await expect
    .element(queueTable.getByRole("columnheader", { name: "Submitted On" }))
    .toBeVisible();
  await expect
    .element(queueTable.getByRole("columnheader", { name: "Status" }))
    .toBeVisible();

  await expect
    .element(screen.getByRole("link", { name: "IA-11111111" }))
    .toHaveAttribute("href", `/applications/${FIRST_ID}`);
  await expect
    .element(screen.getByRole("link", { name: "IA-22222222" }))
    .toHaveAttribute("href", `/applications/${SECOND_ID}`);
  // The status pill is the design system's Tag, which renders as a grid cell.
  await expect
    .element(screen.getByRole("gridcell", { name: "Submitted" }))
    .toBeVisible();
  await expect
    .element(screen.getByRole("gridcell", { name: "Accepted" }))
    .toBeVisible();
});

test("splits the queue from files under review or decided, naming the assignee", async () => {
  stubApi({
    status: 200,
    body: {
      payload: [
        { ...queue[0], status: "RETURNED", inQueue: true },
        {
          ...queue[1],
          status: "UNDER_REVIEW",
          assignee: "MWORKER",
          inQueue: false,
        },
      ],
    },
  });
  const screen = await renderQueue();

  const queueTable = screen.getByRole("table", { name: "Queue" });
  const others = screen.getByRole("table", {
    name: "Under review and decided",
  });
  await expect
    .element(queueTable.getByRole("gridcell", { name: "Returned" }))
    .toBeVisible();
  await expect
    .element(queueTable.getByRole("link", { name: "IA-22222222" }))
    .not.toBeInTheDocument();
  await expect
    .element(others.getByRole("gridcell", { name: "Under Review - MWORKER" }))
    .toBeVisible();
});

test("the request number opens the application", async () => {
  stubApi({ status: 200, body: { payload: queue } });
  const screen = await renderQueue();

  await screen.getByRole("link", { name: "IA-11111111" }).click();

  await expect
    .element(screen.getByText(`Review landing ${FIRST_ID}`))
    .toBeVisible();
});

test("says so when nothing has been submitted", async () => {
  stubApi({ status: 200, body: { payload: [] } });
  const screen = await renderQueue();

  await expect
    .element(screen.getByText("No applications have been submitted yet."))
    .toBeVisible();
});

test("reports a failed load", async () => {
  stubApi({ status: 500, body: { status: 500, detail: "Database down" } });
  const screen = await renderQueue();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("Could not load the submitted applications");
});
