import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import type {
  ClientSearchResultPayload,
  ClientSummaryPayload,
} from "@/api/clientSearch";
import ClientSearch from "@/widgets/support/ClientSearch";

// Support View client search (MYSS-275): the worker searches by one or more
// fields and sees each match well enough to tell them apart, the SIN masked;
// no criteria, or a part SIN, is refused before any request; no match says so.

const ada: ClientSummaryPayload = {
  id: "11111111-2222-3333-4444-555555555555",
  firstName: "Ada",
  lastName: "Lovelace",
  bceidUsername: "ADALOVE",
  sinLastThree: "833",
  dateOfBirth: "1990-01-01",
  signInType: "BCEID",
};

const override: ClientSummaryPayload = {
  id: "22222222-3333-4444-5555-666666666666",
  firstName: "Augusta",
  lastName: "Lovelace",
  bceidUsername: null,
  sinLastThree: "286",
  dateOfBirth: "1985-06-15",
  signInType: "WORKER_OVERRIDE",
};

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function stubSearch(payload: ClientSearchResultPayload) {
  return vi
    .spyOn(window, "fetch")
    .mockImplementation(async () => json(200, { payload }));
}

function renderSearch() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ClientSearch />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("searches by the fields filled in and shows each match, the SIN masked", async () => {
  const fetchMock = stubSearch({ clients: [ada, override], hasMore: false });
  const screen = await renderSearch();

  await screen.getByRole("textbox", { name: "First name" }).fill("ada");
  await screen.getByRole("textbox", { name: "Last name" }).fill("love");
  await screen.getByRole("button", { name: "Search" }).click();

  await expect
    .element(screen.getByText("2 matching clients"))
    .toBeVisible();
  const row = screen.getByRole("row").filter({ hasText: "ADALOVE" });
  await expect.element(row).toHaveTextContent("Lovelace, Ada");
  await expect.element(row).toHaveTextContent("BCeID");
  await expect.element(row).toHaveTextContent("***-***-833");
  await expect.element(row).toHaveTextContent("1990-01-01");
  const other = screen.getByRole("row").filter({ hasText: "Augusta" });
  await expect.element(other).toHaveTextContent("Worker override");
  await expect.element(other).toHaveTextContent("—");
  expect(String(fetchMock.mock.calls[0][0])).toMatch(
    /\/v1\/support\/clients\?firstName=ada&lastName=love$/,
  );
});

test("asks for at least one field and sends nothing when every field is blank", async () => {
  const fetchMock = stubSearch({ clients: [], hasMore: false });
  const screen = await renderSearch();

  await screen.getByRole("textbox", { name: "First name" }).fill("   ");
  await screen.getByRole("button", { name: "Search" }).click();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("Enter at least one search field.");
  expect(fetchMock).not.toHaveBeenCalled();
});

test("asks for the full SIN and sends nothing for a part one", async () => {
  const fetchMock = stubSearch({ clients: [], hasMore: false });
  const screen = await renderSearch();

  await screen.getByRole("textbox", { name: /SIN/ }).fill("833");
  await screen.getByRole("button", { name: "Search" }).click();

  await expect
    .element(screen.getByText("Enter the full 9-digit SIN.").first())
    .toBeVisible();
  expect(fetchMock).not.toHaveBeenCalled();
});

test("says so when no client matches, and shows no table", async () => {
  stubSearch({ clients: [], hasMore: false });
  const screen = await renderSearch();

  await screen.getByRole("textbox", { name: "BCeID user name" }).fill("nobody");
  await screen.getByRole("button", { name: "Search" }).click();

  await expect
    .element(screen.getByText("No matching clients found."))
    .toBeVisible();
  await expect.element(screen.getByRole("table")).not.toBeInTheDocument();
});

test("says when there are more matches than shown", async () => {
  stubSearch({ clients: [ada], hasMore: true });
  const screen = await renderSearch();

  await screen.getByRole("textbox", { name: "Last name" }).fill("l");
  await screen.getByRole("button", { name: "Search" }).click();

  await expect
    .element(screen.getByText(/Add more search fields to narrow the search/))
    .toBeVisible();
});

test("clear empties the fields and the results", async () => {
  stubSearch({ clients: [ada], hasMore: false });
  const screen = await renderSearch();

  await screen.getByRole("textbox", { name: "Last name" }).fill("love");
  await screen.getByRole("button", { name: "Search" }).click();
  await expect.element(screen.getByRole("table")).toBeVisible();

  await screen.getByRole("button", { name: "Clear" }).click();

  await expect.element(screen.getByRole("table")).not.toBeInTheDocument();
  await expect
    .element(screen.getByRole("textbox", { name: "Last name" }))
    .toHaveValue("");
});

test("shows the API's refusal, and an outage, as an alert", async () => {
  vi.spyOn(window, "fetch")
    .mockResolvedValueOnce(
      json(400, { keyword: "SUPPORT.CLIENT_SEARCH.SIN_INVALID" }),
    )
    .mockResolvedValueOnce(json(500, {}));
  const screen = await renderSearch();

  await screen.getByRole("textbox", { name: /SIN/ }).fill("050 082 833");
  await screen.getByRole("button", { name: "Search" }).click();
  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("Enter the full 9-digit SIN.");

  await screen.getByRole("textbox", { name: /SIN/ }).fill("046 454 286");
  await screen.getByRole("button", { name: "Search" }).click();
  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent(/Could not search for clients/);
});
