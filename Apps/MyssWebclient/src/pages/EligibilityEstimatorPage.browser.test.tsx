import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import EligibilityEstimatorPage from "@/pages/EligibilityEstimatorPage";

// End-to-end of the estimator page against a stubbed anonymous API: it renders
// the served spec, reveals the spouse section on Married, computes the estimate
// CLIENT-SIDE, and short-circuits on a pre-check "No" without computing. The
// spec + rates fetches are mocked; the calculation is the real code.
//
// (Runs in the browser project — `npm run test:browser`. The cloud sandbox
// can't launch the browser runner, so this is verified on the dev machine.)
//
// Form.io names its radio groups data[<key>][<random-per-render suffix>], so we
// drive controls by ACCESSIBLE ROLE (from the option label), not by DOM name.
// The three Yes/No groups render in spec order — residesInBc(0),
// hasEligibleStatus(1), pwd(2) — so a duplicate "Yes"/"No" is picked by .nth().

const yesNo = [
  { label: "Yes", value: "true" },
  { label: "No", value: "false" },
];
const partneredConditional = {
  json: { in: [{ var: "data.relationshipStatus" }, ["married", "marriagelike"]] },
};

/** A faithful slice of the v2 estimator spec — the real keys + the conditional. */
const estimatorSpec = {
  formSpecId: "eligibility-estimator",
  version: 2,
  title: "Eligibility Estimator",
  spec: {
    display: "form",
    components: [
      {
        type: "radio",
        key: "residesInBc",
        label: "Do you currently reside in British Columbia?",
        input: true,
        values: yesNo,
        validate: { required: true },
      },
      {
        type: "radio",
        key: "hasEligibleStatus",
        label: "Do you have a status that allows you to live in Canada?",
        input: true,
        values: yesNo,
        validate: { required: true },
      },
      {
        type: "panel",
        key: "statusHelp",
        title: 'What does "status that allows you to live in Canada" mean?',
        collapsible: true,
        collapsed: true,
        input: false,
        components: [
          {
            type: "content",
            key: "statusHelpBody",
            input: false,
            html: "<p>For example: a Canadian citizen, permanent resident, Convention refugee, or another immigration status that allows you to live in Canada.</p>",
          },
        ],
      },
      {
        type: "radio",
        key: "relationshipStatus",
        label: "What is your relationship status?",
        input: true,
        values: [
          { label: "Single and Never Married", value: "single" },
          { label: "Married", value: "married" },
          { label: "Marriage-Like Relationship", value: "marriagelike" },
          { label: "Divorced", value: "divorced" },
          { label: "Separated", value: "separated" },
          { label: "Widowed", value: "widowed" },
        ],
        validate: { required: true },
      },
      {
        type: "number",
        key: "dependentChildren",
        label: "How many dependent children under the age of 19 live with you?",
        input: true,
        defaultValue: 0,
        validate: { min: 0 },
      },
      {
        type: "radio",
        key: "pwd",
        label:
          "Do you plan to apply for the Persons with Disabilities (PWD) designation?",
        input: true,
        values: yesNo,
        validate: { required: true },
      },
      {
        type: "radio",
        key: "partnerPwd",
        label:
          "Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?",
        input: true,
        values: yesNo,
        conditional: partneredConditional,
      },
      {
        type: "content",
        key: "assetsSectionHeading",
        input: false,
        html: "<h2>Do you have assets or receive income?</h2>",
      },
      {
        type: "number",
        key: "monthlyIncome",
        label: "Your Monthly Income",
        input: true,
        defaultValue: 0,
        validate: { min: 0 },
      },
      {
        type: "content",
        key: "spouseSectionHeading",
        input: false,
        html: "<h2>Does your spouse have assets or receive income?</h2>",
        conditional: partneredConditional,
      },
      {
        type: "number",
        key: "partnerMonthlyIncome",
        label: "Spouse's Monthly Income",
        input: true,
        defaultValue: 0,
        validate: { min: 0 },
        conditional: partneredConditional,
      },
      {
        type: "button",
        key: "submit",
        action: "submit",
        label: "Get Estimate",
        input: true,
      },
    ],
  },
};

/** MYSS-25 August-2023 rate table (matches the seed + parked C#). */
const rates = {
  effectiveDate: "2023-08-01",
  incomeRows: [
    { familySize: 1, a: 0, b: 1060, c: 0, d: 1535.5, e: 0 },
    { familySize: 2, a: 1650, b: 1405, c: 2290.5, d: 1880.5, e: 2766 },
    { familySize: 3, a: 1845, b: 1500, c: 2485.5, d: 1975.5, e: 2961 },
    { familySize: 4, a: 1895, b: 1550, c: 2535.5, d: 2025.5, e: 3011 },
    { familySize: 5, a: 1945, b: 1600, c: 2585.5, d: 2075.5, e: 3061 },
    { familySize: 6, a: 1995, b: 1650, c: 2635.5, d: 2125.5, e: 3111 },
    { familySize: 7, a: 2045, b: 1700, c: 2685.5, d: 2175.5, e: 3161 },
  ],
  assetLimits: { a: 5000, b: 10000, c: 100000, d: 200000 },
};

function stubEstimatorApi() {
  vi.spyOn(window, "fetch").mockImplementation(async (input) => {
    const url = String(input);
    if (url.endsWith("/v1/EligibilityEstimator/spec")) {
      return new Response(JSON.stringify({ payload: estimatorSpec }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (url.endsWith("/v1/EligibilityEstimator/rates")) {
      return new Response(JSON.stringify({ payload: rates }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    throw new Error(`Unexpected fetch in test: ${url}`);
  });
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <EligibilityEstimatorPage />
    </QueryClientProvider>,
  );
}

beforeEach(() => stubEstimatorApi());
afterEach(() => vi.restoreAllMocks());

test("renders the form from the served spec (not the old hardcoded components)", async () => {
  const screen = await renderPage();

  await expect
    .element(screen.getByText("What is your relationship status?"))
    .toBeVisible();
  // Page chrome from the 0826 design.
  await expect
    .element(screen.getByText("Your information is private"))
    .toBeVisible();
  await expect.element(screen.getByText("*All fields are required.")).toBeVisible();

  // 0827 seed layout: the status explainer is now an inline collapsible panel
  // (was page chrome above the form), and the applicant money block has a heading.
  await expect.element(screen.getByText(/What does .* mean\?/)).toBeVisible();
  await expect
    .element(screen.getByText("Do you have assets or receive income?"))
    .toBeVisible();
});

test("reveals the spouse section on Married", async () => {
  const screen = await renderPage();
  await expect
    .element(screen.getByText("What is your relationship status?"))
    .toBeVisible();

  await screen.getByRole("radio", { name: /^Married$/ }).click();

  await expect
    .element(screen.getByText(/Spouse's Monthly Income/))
    .toBeVisible();
  await expect
    .element(
      screen.getByText(
        /Does your spouse plan to apply for the Persons with Disabilities/,
      ),
    )
    .toBeVisible();
  await expect
    .element(screen.getByText("Does your spouse have assets or receive income?"))
    .toBeVisible();
});

test("computes an eligible estimate in the browser (single, no PWD, no income → $1,060.00)", async () => {
  const screen = await renderPage();
  await expect
    .element(screen.getByText("What is your relationship status?"))
    .toBeVisible();

  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus
  await screen.getByRole("radio", { name: "Single and Never Married" }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect
    .element(screen.getByText("You may be eligible for assistance"))
    .toBeVisible();
  await expect.element(screen.getByText(/\$1,060\.00/)).toBeVisible();
  await expect
    .element(screen.getByText("How your estimate was calculated"))
    .toBeVisible();
});

test("shows the ineligible ($0) result with the hardship link when income exceeds the limit", async () => {
  const screen = await renderPage();
  await expect
    .element(screen.getByText("What is your relationship status?"))
    .toBeVisible();

  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus
  await screen.getByRole("radio", { name: "Single and Never Married" }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd
  // Single type-B income limit is 1060 → 2000 is over the limit → ineligible.
  await screen.getByLabelText("Your Monthly Income").fill("2000");

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect
    .element(screen.getByText("You may not be eligible for assistance"))
    .toBeVisible();
  await expect.element(screen.getByText("Why is my estimate $0?")).toBeVisible();
  await expect
    .element(
      screen.getByRole("link", {
        name: "Contact us to find out more about this kind of support.",
      }),
    )
    .toBeVisible();
});

test("a pre-check 'No' short-circuits with no estimate computed", async () => {
  const screen = await renderPage();
  await expect
    .element(screen.getByText("What is your relationship status?"))
    .toBeVisible();

  await screen.getByRole("radio", { name: /^No$/ }).nth(0).click(); // residesInBc = No
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await screen.getByRole("radio", { name: "Single and Never Married" }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  // Prescreen-only copy proves we took the short-circuit branch…
  await expect
    .element(
      screen.getByText(
        "To receive assistance you must live in British Columbia and have a status that allows you to live in Canada.",
      ),
    )
    .toBeVisible();
  // …and no monetary estimate was produced.
  expect(document.body.textContent).not.toContain("/ month");
});
