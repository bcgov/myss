import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import type { WorkerOverrideDraftSummaryPayload } from "@/api/workerOverride";
import WorkerOverrideDrafts from "@/widgets/workerOverride/WorkerOverrideDrafts";

// The unsubmitted worker override list (MYSS-322): every worker's drafts as
// the API orders them, each opening its own draft, with who started it and
// a placeholder where a detail is not known yet.

const named: WorkerOverrideDraftSummaryPayload = {
  id: "11111111-2222-3333-4444-555555555555",
  referenceNumber: "IA-11111111",
  clientName: "Ada Lovelace",
  startedBy: "MADMIN",
  createdAt: "2026-11-04T09:00:00Z",
  updatedAt: "2026-11-05T09:30:00Z",
};

const blank: WorkerOverrideDraftSummaryPayload = {
  id: "22222222-3333-4444-5555-666666666666",
  referenceNumber: "IA-22222222",
  clientName: null,
  startedBy: null,
  createdAt: "2026-11-03T10:00:00Z",
  updatedAt: "2026-11-03T10:00:00Z",
};

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function stubList(response: Response) {
  return vi.spyOn(window, "fetch").mockResolvedValue(response);
}

function renderList() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <WorkerOverrideDrafts />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("lists every draft in the API's order, each opening its own draft", async () => {
  const fetchMock = stubList(json(200, { payload: [named, blank] }));
  const screen = await renderList();

  const links = screen.getByRole("link");
  await expect.element(links.first()).toHaveTextContent("IA-11111111");
  await expect
    .element(screen.getByRole("link", { name: "IA-11111111" }))
    .toHaveAttribute("href", `/worker-override/${named.id}`);
  await expect
    .element(screen.getByRole("link", { name: "IA-22222222" }))
    .toHaveAttribute("href", `/worker-override/${blank.id}`);
  expect(links.elements()).toHaveLength(2);
  expect(String(fetchMock.mock.calls[0][0])).toMatch(
    /\/v1\/intake\/worker-override\/applications\/drafts$/,
  );
});

test("shows the client and who started it", async () => {
  stubList(json(200, { payload: [named] }));
  const screen = await renderList();

  const row = screen.getByRole("row").filter({ hasText: "IA-11111111" });
  await expect.element(row).toHaveTextContent("Ada Lovelace");
  await expect.element(row).toHaveTextContent("MADMIN");
  await expect
    .element(row)
    .toHaveTextContent(new Date(named.updatedAt).toLocaleString());
});

test("says what is not known yet rather than leaving a gap", async () => {
  stubList(json(200, { payload: [blank] }));
  const screen = await renderList();

  const row = screen.getByRole("row").filter({ hasText: "IA-22222222" });
  await expect.element(row).toHaveTextContent("Not entered yet");
  await expect.element(row).toHaveTextContent("—");
});

test("says so when there are no drafts", async () => {
  stubList(json(200, { payload: [] }));
  const screen = await renderList();

  await expect
    .element(screen.getByText("There are no unsubmitted applications."))
    .toBeVisible();
  await expect.element(screen.getByRole("table")).not.toBeInTheDocument();
});

test("says so when the list cannot be loaded", async () => {
  stubList(json(403, {}));
  const screen = await renderList();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent(/Could not load the unsubmitted applications/);
});
