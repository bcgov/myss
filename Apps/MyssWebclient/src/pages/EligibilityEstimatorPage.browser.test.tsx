import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";

import EligibilityEstimatorPage from "@/pages/EligibilityEstimatorPage";
import { registerBcgovComponents } from "@/formio/bcgovComponents";

// End-to-end of the estimator page against a stubbed anonymous API, driving the
// MYSS-206 v3 spec: progressive disclosure (Q2 reveals once Q1 is answered; the
// remaining questions reveal only when Q2 = Yes), the inline "not eligible"
// warning when Q2 = No, and the client-side estimate. The spec + rates fetches
// are mocked; the calculation + conditional logic are the real code.
//
// (Runs in the browser project — `npm run test:browser`. The cloud sandbox
// can't launch the browser runner, so this is verified on the dev machine.)
//
// Form.io names its radio groups data[<key>][<random-per-render suffix>], so we
// drive controls by ACCESSIBLE ROLE (from the option label), not by DOM name.
// The Yes/No groups render (and reveal) in spec order — residesInBc(0), then
// hasEligibleStatus(1) after Q1, then pwd(2) after Q2=Yes, then partnerPwd(3)
// after Married — so a duplicate "Yes"/"No" is picked by .nth().

const yesNo = [
  { label: "Yes", value: "true" },
  { label: "No", value: "false" },
];
const partneredConditional = {
  json: { in: [{ var: "data.relationshipStatus" }, ["married", "marriagelike"]] },
};
// v3 gates (see form-spec-seed-data.ts).
const q1AnsweredConditional = {
  json: { in: [{ var: "data.residesInBc" }, ["true", "false"]] },
};
const hasStatusConditional = { show: true, when: "hasEligibleStatus", eq: "true" };

/** A faithful slice of the v3 estimator spec — real keys + the v3 conditionals. */
const estimatorSpec = {
  formSpecId: "eligibility-estimator",
  version: 3,
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
        dataType: "string",
        validate: { required: true },
      },
      {
        type: "radio",
        key: "hasEligibleStatus",
        label: "Do you have a status that allows you to live in Canada?",
        // (No Q2 tooltip — removed from the v3 seed; help lives in the accordion.)
        input: true,
        values: yesNo,
        dataType: "string",
        validate: { required: true },
        conditional: q1AnsweredConditional,
      },
      {
        type: "bcgovAccordion",
        key: "statusHelp",
        input: false,
        accordionLabel:
          'What does "status that allows you to live in Canada" mean?',
        accordionBody:
          "<p>To be eligible for assistance, your status must meet the citizenship and residency requirements.</p>",
        conditional: q1AnsweredConditional,
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
        conditional: hasStatusConditional,
      },
      {
        type: "number",
        key: "dependentChildren",
        label: "How many dependent children under the age of 19 live with you?",
        input: true,
        defaultValue: 0,
        validate: { min: 0 },
        conditional: hasStatusConditional,
      },
      {
        type: "radio",
        key: "pwd",
        label:
          "Do you plan to apply for the Persons with Disabilities (PWD) designation?",
        input: true,
        values: yesNo,
        validate: { required: true },
        conditional: hasStatusConditional,
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
        conditional: hasStatusConditional,
      },
      {
        type: "number",
        key: "monthlyIncome",
        label: "Your Monthly Income",
        input: true,
        defaultValue: 0,
        validate: { min: 0 },
        conditional: hasStatusConditional,
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
        conditional: hasStatusConditional,
      },
    ],
  },
};

// A spec variant used ONLY by the "blocked submit" regression test: it adds a
// required TEXT field (revealed with the rest of the form) so the test can clear
// it to "" and produce a genuinely blocked submit. A text field has no numeric
// default to fall back on, and the Playwright runner can't deselect a required
// radio — so this is the one reliable way to trigger Form.io's componentError in
// the runner. Kept OUT of the shared spec so it can't perturb other tests.
const estimatorSpecWithRequiredName = {
  ...estimatorSpec,
  spec: {
    ...estimatorSpec.spec,
    components: [
      ...estimatorSpec.spec.components.slice(0, -1), // everything but the submit button
      {
        type: "textfield",
        key: "applicantName",
        label: "Your full name",
        input: true,
        // Default lets the FIRST estimate pass (required satisfied); the test
        // then clears it to "" to force the blocked submit.
        defaultValue: "Test User",
        validate: { required: true },
        conditional: hasStatusConditional,
      },
      estimatorSpec.spec.components[estimatorSpec.spec.components.length - 1], // submit last
    ],
  },
};

// The spec the stubbed API serves; reset before each test, overridden by the one
// test that needs the required-name variant.
let activeSpec: typeof estimatorSpec = estimatorSpec;

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
      return new Response(JSON.stringify({ payload: activeSpec }), {
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

// The v3 spec references the custom bcgovAccordion type; register it once so
// Form.io renders it instead of a blank slot (mirrors main.tsx at app start).
registerBcgovComponents();

beforeEach(() => {
  activeSpec = estimatorSpec; // clean spec by default
  stubEstimatorApi();
});
afterEach(() => vi.restoreAllMocks());

const Q2_LABEL = "Do you have a status that allows you to live in Canada?";
const RELATIONSHIP_LABEL = "What is your relationship status?";

// --- Progressive disclosure (the MYSS-206 truth table) --------------------

test("initially shows only Q1 — Q2 and the remaining questions are hidden", async () => {
  const screen = await renderPage();

  await expect
    .element(screen.getByText("Do you currently reside in British Columbia?"))
    .toBeVisible();
  // Page chrome from the 0826 design.
  await expect
    .element(screen.getByText("Your information is private"))
    .toBeVisible();
  await expect.element(screen.getByText("*All fields are required.")).toBeVisible();

  // Nothing past Q1 is present yet.
  expect(document.body.textContent).not.toContain(Q2_LABEL);
  expect(document.body.textContent).not.toContain(RELATIONSHIP_LABEL);
});

test("answering Q1 reveals Q2 (+ the help accordion) but not the remaining questions", async () => {
  const screen = await renderPage();
  await expect
    .element(screen.getByText("Do you currently reside in British Columbia?"))
    .toBeVisible();

  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc

  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();
  await expect.element(screen.getByText(/What does .* mean\?/)).toBeVisible();
  // Remaining questions stay hidden until Q2 = Yes.
  expect(document.body.textContent).not.toContain(RELATIONSHIP_LABEL);
});

test("Q2 = Yes reveals the remaining questions and Get Estimate", async () => {
  const screen = await renderPage();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();

  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes

  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await expect
    .element(screen.getByText("Do you have assets or receive income?"))
    .toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: "Get Estimate" }))
    .toBeVisible();
});

test("Q2 = No shows the inline warning and keeps the remaining questions hidden", async () => {
  const screen = await renderPage();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();

  await screen.getByRole("radio", { name: /^No$/ }).nth(1).click(); // hasEligibleStatus = No

  await expect
    .element(screen.getByText("You might not be eligible for assistance"))
    .toBeVisible();
  expect(document.body.textContent).not.toContain(RELATIONSHIP_LABEL);

  // 0901 ee-05: both links in the warning box are clickable, pointing at the
  // real gov.bc.ca pages (residency requirements + hardship "Contact us").
  const residencyLink = screen.getByRole("link", {
    name: "residency requirements",
  });
  await expect.element(residencyLink).toBeVisible();
  await expect
    .element(residencyLink)
    .toHaveAttribute("href", expect.stringContaining("citizenship-requirements"));
  const hardshipLink = screen.getByRole("link", {
    name: "Contact us to find out more about this kind of support.",
  });
  await expect.element(hardshipLink).toBeVisible();
  await expect
    .element(hardshipLink)
    .toHaveAttribute("href", expect.stringContaining("access-services"));
});

// --- Estimate flows -------------------------------------------------------

test("MYSS-206: Q1 = No but Q2 = Yes proceeds to a full estimate (residency no longer blocks)", async () => {
  const screen = await renderPage();
  await screen.getByRole("radio", { name: /^No$/ }).nth(0).click(); // residesInBc = No

  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes

  // Remaining questions reveal — Q1=No does NOT dead-end.
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: "Single and Never Married" }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd = No

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect
    .element(screen.getByText("You may be eligible for assistance"))
    .toBeVisible();
  await expect.element(screen.getByText(/\/ month/)).toBeVisible();
});

test("reveals the spouse section on Married", async () => {
  const screen = await renderPage();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();

  await screen.getByRole("radio", { name: /^Married$/ }).click();

  await expect.element(screen.getByText(/Spouse's Monthly Income/)).toBeVisible();
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
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: "Single and Never Married" }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd = No

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
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: "Single and Never Married" }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd = No
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

test("a couple who leaves the spouse-PWD question blank is blocked, not silently scored as 'No'", async () => {
  const screen = await renderPage();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: /^Married$/ }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd (applicant) = No

  // Spouse section is revealed. partnerPwd carries no SERVER-side required (that
  // would fail the FormSpecValidator + reject singles), so it is required at
  // RUNTIME (handleFormReady): Form.io blocks the submit and shows an inline
  // field error rather than silently scoring the blank as "No".
  await expect
    .element(
      screen.getByText(
        /Does your spouse plan to apply for the Persons with Disabilities/,
      ),
    )
    .toBeVisible();

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  // Blocked INLINE on the spouse field (runtime-required), not via the old
  // page-level alert — and no estimate is produced.
  await expect
    .element(screen.getByText("Please select an option."))
    .toBeVisible();
  expect(document.body.textContent).not.toContain("/ month");
});

test("the same couple gets an estimate once the spouse-PWD question is answered", async () => {
  const screen = await renderPage();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: /^Married$/ }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd (applicant) = No

  await expect
    .element(
      screen.getByText(
        /Does your spouse plan to apply for the Persons with Disabilities/,
      ),
    )
    .toBeVisible();
  await screen.getByRole("radio", { name: /^No$/ }).nth(3).click(); // partnerPwd = No

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect
    .element(screen.getByText("You may be eligible for assistance"))
    .toBeVisible();
  await expect.element(screen.getByText(/\/ month/)).toBeVisible();
});

// --- Regression: the result only changes on a "Get Estimate" click ---------

/** Married + both PWD, no income → family size 2, column e = $2,766.00. */
async function reachCoupleEstimate(screen: Awaited<ReturnType<typeof renderPage>>) {
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: /^Married$/ }).click();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(2).click(); // pwd (applicant) = Yes
  await expect
    .element(
      screen.getByText(
        /Does your spouse plan to apply for the Persons with Disabilities/,
      ),
    )
    .toBeVisible();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(3).click(); // partnerPwd = Yes
  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect.element(screen.getByText(/\$2,766\.00/)).toBeVisible();
}

// A plain field edit must NOT flicker or clear the shown result — the estimate
// only refreshes when the user clicks Get Estimate again.
test("editing a field after an estimate leaves the result on screen", async () => {
  const screen = await renderPage();
  await reachCoupleEstimate(screen);

  await screen.getByLabelText("Spouse's Monthly Income").fill("50");

  // Still there — no clear-on-change flicker.
  await expect.element(screen.getByText(/\$2,766\.00/)).toBeVisible();
  await expect
    .element(screen.getByText("Your eligibility estimate"))
    .toBeVisible();
});

// Regression (design gap): flipping Q2 to "No" after an estimate reveals the
// screen-fail warning AND must clear the now-contradictory result card.
test("changing Q2 to No after an estimate hides the stale result under the warning", async () => {
  const screen = await renderPage();
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(0).click(); // residesInBc = Yes
  await screen.getByRole("radio", { name: /^Yes$/ }).nth(1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("radio", { name: "Single and Never Married" }).click();
  await screen.getByRole("radio", { name: /^No$/ }).nth(2).click(); // pwd = No
  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect.element(screen.getByText(/\$1,060\.00/)).toBeVisible();

  // Flip Q2 to No → the warning appears and the stale estimate is cleared.
  await screen.getByRole("radio", { name: /^No$/ }).nth(1).click(); // hasEligibleStatus = No

  await expect
    .element(screen.getByText("You might not be eligible for assistance"))
    .toBeVisible();
  // Async, retried — the clear runs in a useEffect one tick after the warning
  // renders, so a synchronous body check here would race it.
  await expect.element(screen.getByText(/\/ month/)).not.toBeInTheDocument();
});

// The original bug: after an estimate, editing the form into an invalid state
// then re-clicking Get Estimate left the previous result on screen. Form.io v5
// short-circuits a blocked submit — it emits only a per-field `componentError`
// (no submit/submitError/error) — so `onSubmit` never runs to refresh, and the
// stale card must be cleared on componentError. Uses the required-name spec
// variant so we can clear a text field to "" and genuinely block the submit
// (the runner can't deselect a radio; empty numbers fall back to their default).
test("a submit blocked by validation hides the previous result", async () => {
  activeSpec = estimatorSpecWithRequiredName;
  const screen = await renderPage();
  await reachCoupleEstimate(screen);

  await screen.getByLabelText("Your full name").fill(""); // required text → empty

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  // Blocked submit → Form.io emits componentError → the page clears the stale
  // result. Async, retried assertions so nothing races the re-render.
  await expect
    .element(screen.getByText("Your eligibility estimate"))
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByText(/\$2,766\.00/))
    .not.toBeInTheDocument();
});
