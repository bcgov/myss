import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, expect, test, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import type { ApplicationSummaryPayload } from "@/api/intake";
import { paths } from "@/routes/paths";
import ServiceRequestsPage from "./ServiceRequestsPage";

const draft: ApplicationSummaryPayload = {
  id: "11111111-2222-3333-4444-555555555555",
  referenceNumber: "IA-11111111",
  status: "DRAFT",
  version: 2,
  formSpecId: "application-for-assistance",
  formSpecVersion: 3,
  createdAt: "2026-09-28T10:00:00Z",
  updatedAt: "2026-09-28T10:00:00Z",
  submittedAt: null,
};
const submitted: ApplicationSummaryPayload = {
  ...draft,
  id: "22222222-3333-4444-5555-666666666666",
  referenceNumber: "IA-22222222",
  status: "SUBMITTED",
  submittedAt: "2026-09-29T10:00:00Z",
};

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[paths.serviceRequests]}>
        <Routes>
          <Route
            path={paths.serviceRequests}
            element={<ServiceRequestsPage />}
          />
          <Route path={paths.afaServiceRequest} element={<p>AFA detail</p>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => vi.restoreAllMocks());

test("shows only owned AFA drafts and submissions with distinct actions", async () => {
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/forms/application-for-assistance/spec")) {
      return json(200, { payload: {} });
    }
    if (url.endsWith("/v1/intake/applications/afa") && !init?.method) {
      return json(200, { payload: [draft, submitted] });
    }
    throw new Error(`Unexpected fetch: ${url}`);
  });

  const screen = await renderPage();
  await expect
    .element(screen.getByRole("heading", { name: "Service Requests" }))
    .toBeVisible();
  await expect
    .element(screen.getByRole("link", { name: /Continue application/ }))
    .toHaveAttribute("href", `/service-requests/${draft.id}`);
  await expect
    .element(screen.getByRole("link", { name: /View application/ }))
    .toHaveAttribute("href", `/service-requests/${submitted.id}`);
  await expect
    .element(
      screen.getByRole("button", { name: "Start Application for Assistance" }),
    )
    .toBeVisible();
});

test("hides unpublished AFA forms from Service Requests", async () => {
  vi.spyOn(window, "fetch").mockImplementation(async (input) =>
    String(input).endsWith("/v1/forms/application-for-assistance/spec")
      ? json(404, {})
      : json(200, { payload: [] }),
  );
  const screen = await renderPage();
  await expect
    .element(screen.getByRole("heading", { name: "Service Requests" }))
    .toBeVisible();
  await expect
    .element(screen.getByText(/not available yet/))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByText(/no Application for Assistance requests yet/))
    .not.toBeInTheDocument();
  await expect
    .element(
      screen.getByRole("button", { name: "Start Application for Assistance" }),
    )
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("heading", { name: "Your current applications" }))
    .not.toBeInTheDocument();
});

test("does not mistake a content-engine error for an unpublished form", async () => {
  vi.spyOn(window, "fetch").mockImplementation(async (input) =>
    String(input).endsWith("/v1/forms/application-for-assistance/spec")
      ? json(502, { detail: "Content engine unavailable" })
      : json(200, { payload: [] }),
  );
  const screen = await renderPage();
  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent(/Could not check available applications/);
  await expect
    .element(
      screen.getByRole("button", { name: "Start Application for Assistance" }),
    )
    .not.toBeInTheDocument();
});

test("starts an AFA and opens the detail page", async () => {
  const posts: string[] = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/forms/application-for-assistance/spec")) {
      return json(200, { payload: {} });
    }
    if (
      url.endsWith("/v1/intake/applications/afa") &&
      init?.method === "POST"
    ) {
      posts.push(url);
      return json(201, { payload: draft });
    }
    return json(200, { payload: [] });
  });
  const screen = await renderPage();
  await screen
    .getByRole("button", { name: "Start Application for Assistance" })
    .click();
  await expect.element(screen.getByText("AFA detail")).toBeVisible();
  expect(posts).toHaveLength(1);
});
