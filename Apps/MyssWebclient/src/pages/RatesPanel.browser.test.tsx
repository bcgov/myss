import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import RatesPanel from "./RatesPanel";
import { getEstimatorRates } from "@/api/eligibility";
import type { EligibilityRates } from "@/api/eligibility";
import { RatesRejectedError, saveEstimatorRates } from "@/api/eligibilityRatesAdmin";
import type { SaveEstimatorRatesInput } from "@/api/eligibilityRatesAdmin";

// The rates panel against a stubbed rates read and save; the rest of both API
// modules stays real.

vi.mock("@/api/eligibility", async (importActual) => ({
  ...(await importActual<typeof import("@/api/eligibility")>()),
  getEstimatorRates: vi.fn(),
}));

vi.mock("@/api/eligibilityRatesAdmin", async (importActual) => ({
  ...(await importActual<typeof import("@/api/eligibilityRatesAdmin")>()),
  saveEstimatorRates: vi.fn(),
}));

const mockGetRates = vi.mocked(getEstimatorRates);
const mockSave = vi.mocked(saveEstimatorRates);

// The seeded table.
const rates: EligibilityRates = {
  effectiveDate: "2026-10-02",
  incomeRows: [
    { familySize: 1, a: 0, b: 1060, c: 0, d: 0, e: 1360, f: 0, g: 1535.5, h: 0, i: 0 },
    { familySize: 2, a: 1650, b: 1405, c: 2200, d: 1950, e: 1705, f: 2290.5, g: 1880.5, h: 2766, i: 2590.5 },
    { familySize: 3, a: 1845, b: 1500, c: 2395, d: 2145, e: 1800, f: 2485.5, g: 1975.5, h: 2961, i: 2785.5 },
    { familySize: 4, a: 1895, b: 1550, c: 2445, d: 2195, e: 1850, f: 2535.5, g: 2025.5, h: 3011, i: 2835.5 },
    { familySize: 5, a: 1945, b: 1600, c: 2495, d: 2245, e: 1900, f: 2585.5, g: 2075.5, h: 3061, i: 2885.5 },
    { familySize: 6, a: 1995, b: 1650, c: 2545, d: 2295, e: 1950, f: 2635.5, g: 2125.5, h: 3111, i: 2935.5 },
    { familySize: 7, a: 2045, b: 1700, c: 2595, d: 2345, e: 2000, f: 2685.5, g: 2175.5, h: 3161, i: 2985.5 },
  ],
  assetLimits: { a: 5000, b: 10000, c: 100000, d: 200000 },
};

const E_AT_2 = "Family size 2, category E (Income Assistance 65+)";
const B_AT_1 = "Family size 1, category B (Income Assistance)";
const ASSET_B = "Asset limit, category B";
const ASSET_C = "Asset limit, category C";

let queryClient: QueryClient;

function renderPanel() {
  queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RatesPanel />
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.clearAllMocks();
});

describe("RatesPanel", () => {
  it("renders the income and asset limits", async () => {
    mockGetRates.mockResolvedValue(rates);

    const screen = await renderPanel();

    await expect.element(screen.getByText("Eligibility rates")).toBeVisible();
    await expect.element(screen.getByText("Effective 2026-10-02")).toBeVisible();
    // An income-limit cell and an asset-limit cell.
    await expect.element(screen.getByText("$1,060")).toBeVisible();
    await expect.element(screen.getByText("$100,000")).toBeVisible();
  });

  it("groups the income columns under three headings", async () => {
    mockGetRates.mockResolvedValue(rates);

    const screen = await renderPanel();
    const income = screen.getByRole("table", { name: "Monthly income limits" });

    // Found by text: Vitest's locator engine treats a scope="colgroup" header as
    // a cell, although browsers expose it as a column header.
    for (const [group, span] of [
      ["Income Assistance", "2"],
      ["Income Assistance 65+", "3"],
      ["Disability Assistance", "4"],
    ]) {
      const heading = income.getByText(group, { exact: true });
      await expect.element(heading).toBeVisible();
      await expect.element(heading).toHaveAttribute("scope", "colgroup");
      await expect.element(heading).toHaveAttribute("colspan", span);
    }
    const colgroups = income.element().querySelectorAll("colgroup");
    expect(Array.from(colgroups, (colgroup) => colgroup.span)).toEqual([1, 2, 3, 4]);
  });

  it("shows a letter heading for each of the nine client types", async () => {
    mockGetRates.mockResolvedValue(rates);

    const screen = await renderPanel();
    const income = screen.getByRole("table", { name: "Monthly income limits" });

    for (const letter of ["A", "B", "C", "D", "E", "F", "G", "H", "I"]) {
      await expect
        .element(income.getByRole("columnheader", { name: letter, exact: true }))
        .toBeVisible();
    }
  });

  it("shows N/A, read as Not applicable, in the six couple cells at family size 1", async () => {
    mockGetRates.mockResolvedValue(rates);

    const screen = await renderPanel();
    const income = screen.getByRole("table", { name: "Monthly income limits" });
    await expect.element(income).toBeVisible();

    const notApplicable = income.getByRole("cell", { name: "Not applicable", exact: true });
    expect(notApplicable.elements()).toHaveLength(6);
    expect(income.getByText("N/A").elements()).toHaveLength(6);
    // The single columns at family size 1 still show their amounts.
    await expect.element(income.getByText("$1,360")).toBeVisible();
    await expect.element(income.getByText("$1,535.5")).toBeVisible();
    // A couple column at family size 2 shows its amount, not N/A.
    await expect.element(income.getByText("$2,590.5")).toBeVisible();
  });

  it("decides N/A by column, not by a zero amount", async () => {
    const [fs1, fs2] = rates.incomeRows;
    mockGetRates.mockResolvedValue({ ...rates, incomeRows: [fs1, { ...fs2, b: 0 }] });

    const screen = await renderPanel();
    const income = screen.getByRole("table", { name: "Monthly income limits" });

    await expect.element(income.getByText("$0", { exact: true })).toBeVisible();
    const notApplicable = income.getByRole("cell", { name: "Not applicable", exact: true });
    expect(notApplicable.elements()).toHaveLength(6);
  });

  it("does not widen the page on a narrow screen", async () => {
    const { innerWidth, innerHeight } = window;
    await page.viewport(375, 812);
    try {
      mockGetRates.mockResolvedValue(rates);

      const screen = await renderPanel();
      await expect
        .element(screen.getByRole("table", { name: "Monthly income limits" }))
        .toBeVisible();

      const root = document.documentElement;
      expect(root.scrollWidth).toBeLessThanOrEqual(root.clientWidth);
    } finally {
      await page.viewport(innerWidth, innerHeight);
    }
  });

  it("shows the asset limits A to D", async () => {
    mockGetRates.mockResolvedValue(rates);

    const screen = await renderPanel();
    const assets = screen.getByRole("table", { name: "Asset limits" });

    for (const letter of ["A", "B", "C", "D"]) {
      await expect
        .element(assets.getByRole("columnheader", { name: letter, exact: true }))
        .toBeVisible();
    }
    await expect.element(assets.getByText("$5,000")).toBeVisible();
    await expect.element(assets.getByText("$200,000")).toBeVisible();
  });

  it("says rate changes are saved separately from the form", async () => {
    mockGetRates.mockResolvedValue(rates);

    const screen = await renderPanel();

    await expect
      .element(screen.getByText(/saved separately from the form and take effect in the estimator/))
      .toBeVisible();
  });

  it("has one Edit for both tables, which shows 61 inputs and focuses the first", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await expect.element(screen.getByRole("button", { name: "Edit rates" })).toBeVisible();

    expect(screen.getByRole("button", { name: "Edit rates" }).elements()).toHaveLength(1);
    await screen.getByRole("button", { name: "Edit rates" }).click();

    expect(screen.getByRole("textbox").elements()).toHaveLength(61);
    await expect.element(screen.getByRole("textbox", { name: E_AT_2 })).toHaveValue("1705");
    await expect.element(screen.getByRole("textbox", { name: ASSET_B })).toHaveValue("10000");
    await expect.element(screen.getByRole("textbox", { name: B_AT_1 })).toHaveFocus();
    // The N/A cells stay N/A, with no input.
    const income = screen.getByRole("table", { name: "Monthly income limits" });
    expect(income.getByRole("cell", { name: "Not applicable", exact: true }).elements()).toHaveLength(6);
  });

  it("refuses text in an income cell and an asset cell, sending nothing", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: B_AT_1 }).fill("aaa");
    await screen.getByRole("textbox", { name: ASSET_C }).fill("aaa");
    await screen.getByRole("button", { name: "Save rates" }).click();

    const summary = screen.getByRole("alert");
    await expect.element(summary).toHaveTextContent(`${B_AT_1}: Enter a dollar amount`);
    await expect.element(summary).toHaveTextContent(`${ASSET_C}: Enter a dollar amount`);
    await expect.element(screen.getByRole("heading", { name: "There is a problem" })).toHaveFocus();
    const cell = screen.getByRole("textbox", { name: B_AT_1 });
    await expect.element(cell).toHaveAttribute("aria-invalid", "true");
    await expect.element(cell).toHaveAccessibleDescription("Use digits, up to 2 decimals.");
    expect(mockSave).not.toHaveBeenCalled();
  });

  it("asks for an amount in a blank cell", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: E_AT_2 }).fill("");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.element(screen.getByRole("alert")).toHaveTextContent(`${E_AT_2}: Enter an amount.`);
    await expect
      .element(screen.getByRole("textbox", { name: E_AT_2 }))
      .toHaveAccessibleDescription("Enter an amount.");
    expect(mockSave).not.toHaveBeenCalled();
  });

  it("moves to the cell when its line in the error summary is selected", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();
    await screen.getByRole("textbox", { name: ASSET_C }).fill("aaa");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await screen.getByRole("button", { name: new RegExp(`^${ASSET_C}:`) }).click();

    await expect.element(screen.getByRole("textbox", { name: ASSET_C })).toHaveFocus();
  });

  it("sends one income edit with the asset limits unchanged, then shows the saved table", async () => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockImplementation(async (input: SaveEstimatorRatesInput) => ({ effectiveDate: "2026-10-06", ...input }));
    const screen = await renderPanel();
    await expect.element(screen.getByRole("status")).toHaveTextContent("");
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.poll(() => mockSave.mock.calls.length).toBe(1);
    const sent = mockSave.mock.calls[0][0];
    expect(sent.incomeRows[1]).toEqual({ ...rates.incomeRows[1], e: 1710 });
    expect(sent.incomeRows[0]).toEqual(rates.incomeRows[0]);
    expect(sent.assetLimits).toEqual(rates.assetLimits);

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent("Eligibility rates saved. The estimator now uses the rates effective 2026-10-06.");
    await expect.element(screen.getByText("$1,710")).toBeVisible();
    await expect.element(screen.getByText("Effective 2026-10-06", { exact: true })).toBeVisible();
    expect(screen.getByRole("textbox").elements()).toHaveLength(0);
    await expect.element(screen.getByRole("button", { name: "Edit rates" })).toHaveFocus();
  });

  it("keeps showing the saved table, without refetching it from a cache that may be stale", async () => {
    // The read still returns the previous table, as another API instance's cache might.
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockImplementation(async (input: SaveEstimatorRatesInput) => ({ effectiveDate: "2026-10-06", ...input }));
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.element(screen.getByText("$1,710")).toBeVisible();
    await expect.element(screen.getByText("Effective 2026-10-06", { exact: true })).toBeVisible();
    expect(mockGetRates).toHaveBeenCalledTimes(1);
  });

  it("sends one asset edit with the income rows unchanged", async () => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockImplementation(async (input: SaveEstimatorRatesInput) => ({ effectiveDate: "2026-10-06", ...input }));
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: ASSET_B }).fill("8000");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.poll(() => mockSave.mock.calls.length).toBe(1);
    const sent = mockSave.mock.calls[0][0];
    expect(sent.assetLimits).toEqual({ ...rates.assetLimits, b: 8000 });
    expect(sent.incomeRows).toEqual(rates.incomeRows);
  });

  it("sends an edit in each table in one call", async () => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockImplementation(async (input: SaveEstimatorRatesInput) => ({ effectiveDate: "2026-10-06", ...input }));
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");
    await screen.getByRole("textbox", { name: ASSET_B }).fill("8000");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.poll(() => mockSave.mock.calls.length).toBe(1);
    const sent = mockSave.mock.calls[0][0];
    expect(sent.incomeRows[1].e).toBe(1710);
    expect(sent.assetLimits.b).toBe(8000);
  });

  it("keeps Save focused and the inputs read-only while a save is in progress", async () => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockReturnValue(new Promise(() => {}));
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();
    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");

    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.element(screen.getByRole("textbox", { name: E_AT_2 })).toHaveAttribute("readonly");
    await expect.element(screen.getByRole("button", { name: "Save rates" })).toHaveFocus();
    await screen.getByRole("button", { name: "Save rates" }).click({ force: true });
    expect(mockSave).toHaveBeenCalledTimes(1);
  });

  it("sends nothing when nothing changed, says so, and leaves edit mode", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: B_AT_1 }).fill("1060.00");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.element(screen.getByRole("status")).toHaveTextContent("No changes to save.");
    expect(mockSave).not.toHaveBeenCalled();
    expect(screen.getByRole("textbox").elements()).toHaveLength(0);
    await expect.element(screen.getByRole("button", { name: "Edit rates" })).toHaveFocus();
  });

  it("refuses to save when the rates changed after Edit", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();
    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");

    // Another admin's save arrives through a refetch.
    mockGetRates.mockResolvedValue({ ...rates, effectiveDate: "2026-10-06" });
    await queryClient.invalidateQueries();
    await expect.element(screen.getByText("Effective 2026-10-06", { exact: true })).toBeVisible();
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("The rates changed since you started editing.");
    expect(mockSave).not.toHaveBeenCalled();
  });

  it("restores both tables on Cancel", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");
    await screen.getByRole("textbox", { name: ASSET_B }).fill("8000");
    await screen.getByRole("button", { name: "Cancel" }).click();

    expect(mockSave).not.toHaveBeenCalled();
    await expect.element(screen.getByText("$1,705")).toBeVisible();
    await expect.element(screen.getByText("$10,000")).toBeVisible();
    await expect.element(screen.getByRole("button", { name: "Edit rates" })).toHaveFocus();
    await screen.getByRole("button", { name: "Edit rates" }).click();
    await expect.element(screen.getByRole("textbox", { name: E_AT_2 })).toHaveValue("1705");
  });

  it("marks only the cells a 422 names, lists other errors as text, and keeps the edits", async () => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockRejectedValue(
      new RatesRejectedError(422, [
        { field: "incomeRows.2.e", keyword: "RATES.AMOUNT.NEGATIVE", message: "Must be 0 or more." },
        { field: "rates", keyword: "RATES.CONTENT_ENGINE.REFUSED", message: "The content engine refused the table." },
      ]),
    );
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");
    await screen.getByRole("button", { name: "Save rates" }).click();

    const summary = screen.getByRole("alert");
    await expect.element(summary).toHaveTextContent(`${E_AT_2}: Must be 0 or more.`);
    await expect.element(summary).toHaveTextContent("The content engine refused the table.");
    expect(summary.getByRole("button").elements()).toHaveLength(1);
    await expect.element(screen.getByRole("textbox", { name: E_AT_2 })).toHaveAttribute("aria-invalid", "true");
    await expect.element(screen.getByRole("textbox", { name: ASSET_B })).not.toHaveAttribute("aria-invalid");
    await expect.element(screen.getByRole("textbox", { name: E_AT_2 })).toHaveValue("1710");
  });

  it("says a 502 save could not be confirmed, keeps the edits, and refetches the table", async () => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockRejectedValue(new RatesRejectedError(502, []));
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: ASSET_B }).fill("8000");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("The save could not be confirmed (error 502). The rates may not have been saved.");
    await expect.element(screen.getByRole("textbox", { name: ASSET_B })).toHaveValue("8000");
    await expect.element(screen.getByRole("heading", { name: "There is a problem" })).toHaveFocus();
    await expect.poll(() => mockGetRates.mock.calls.length).toBe(2);
  });

  it("says a save with no response could not be confirmed", async () => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockRejectedValue(new TypeError("Failed to fetch"));
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: ASSET_B }).fill("8000");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("The save could not be confirmed. The rates may not have been saved.");
  });

  it.each([
    [401, "you are no longer signed in (error 401)"],
    [403, "your account is not allowed to change them (error 403)"],
    [400, "Could not save the rates (error 400)"],
  ])("explains a %i", async (status, text) => {
    mockGetRates.mockResolvedValue(rates);
    mockSave.mockRejectedValue(new RatesRejectedError(status, []));
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();

    await screen.getByRole("textbox", { name: ASSET_B }).fill("8000");
    await screen.getByRole("button", { name: "Save rates" }).click();

    await expect.element(screen.getByRole("alert")).toHaveTextContent(text);
    expect(mockGetRates).toHaveBeenCalledTimes(1);
  });

  it("offers no Edit for a table without one row per family size", async () => {
    mockGetRates.mockResolvedValue({ ...rates, incomeRows: rates.incomeRows.slice(0, 6) });

    const screen = await renderPanel();

    await expect.element(screen.getByText(/so it cannot be edited here/)).toBeVisible();
    expect(screen.getByRole("button", { name: "Edit rates" }).elements()).toHaveLength(0);
  });

  it("keeps the tables and the edits when a refetch fails", async () => {
    mockGetRates.mockResolvedValue(rates);
    const screen = await renderPanel();
    await screen.getByRole("button", { name: "Edit rates" }).click();
    await screen.getByRole("textbox", { name: E_AT_2 }).fill("1710");

    mockGetRates.mockRejectedValue(new Error("Rates fetch failed (500)"));
    await queryClient.invalidateQueries();

    await expect.element(screen.getByText(/could not be refreshed/)).toBeVisible();
    await expect.element(screen.getByRole("textbox", { name: E_AT_2 })).toHaveValue("1710");
    expect(screen.getByRole("alert").elements()).toHaveLength(0);
  });

  it("shows an error when the rates cannot be loaded", async () => {
    mockGetRates.mockRejectedValue(new Error("Rates fetch failed (500)"));

    const screen = await renderPanel();

    const alert = screen.getByRole("alert");
    await expect.element(alert).toBeVisible();
    await expect
      .element(alert)
      .toHaveTextContent("Could not load the eligibility rates");
  });
});
