import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
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

const rates: EligibilityRates = {
  effectiveDate: "2023-08-01",
  incomeRows: [
    { familySize: 1, a: 1060, b: 1260, c: 1360, d: 1460, e: 1560 },
    { familySize: 2, a: 1620, b: 1820, c: 1920, d: 2020, e: 2120 },
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
    await expect.element(screen.getByText("Effective 2023-08-01")).toBeVisible();
    // An income-limit cell and an asset-limit cell.
    await expect.element(screen.getByText("$1,060")).toBeVisible();
    await expect.element(screen.getByText("$100,000")).toBeVisible();
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
