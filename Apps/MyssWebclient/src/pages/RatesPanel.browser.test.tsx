import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { page } from "@vitest/browser/context";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import RatesPanel from "./RatesPanel";
import { getEstimatorRates } from "@/api/eligibility";
import type { EligibilityRates } from "@/api/eligibility";

// The read-only rates table against a stubbed rates read. Only getEstimatorRates
// is mocked; the rest of @/api/eligibility (re-exported by useEligibility) stays
// real.

vi.mock("@/api/eligibility", async (importActual) => ({
  ...(await importActual<typeof import("@/api/eligibility")>()),
  getEstimatorRates: vi.fn(),
}));

const mockGetRates = vi.mocked(getEstimatorRates);

// The first two rows of the seeded table; every non-zero amount is distinct.
const rates: EligibilityRates = {
  effectiveDate: "2026-10-02",
  incomeRows: [
    { familySize: 1, a: 0, b: 1060, c: 0, d: 0, e: 1360, f: 0, g: 1535.5, h: 0, i: 0 },
    { familySize: 2, a: 1650, b: 1405, c: 2200, d: 1950, e: 1705, f: 2290.5, g: 1880.5, h: 2766, i: 2590.5 },
  ],
  assetLimits: { a: 5000, b: 10000, c: 100000, d: 200000 },
};

function renderPanel() {
  const queryClient = new QueryClient({
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
