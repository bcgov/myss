import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "vitest-browser-react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { userEvent } from "vitest/browser";

import EligibilityEstimatorPage from "@/pages/EligibilityEstimatorPage";
import { registerBcgovComponents } from "@/widgets/formio/bcgovComponents";

// End-to-end of the estimator page against a stubbed anonymous API, driving the
// v5 spec: residency is a hard gate (the status question reveals only for "Yes",
// so "No" is terminal), the remaining questions reveal only when status is "Yes",
// the inline "not eligible" warning fires on either "No", plus validation display
// and the client-side estimate. The spec + rates fetches are mocked; the
// calculation, conditional logic and custom components are the real code.
//
// (Runs in the browser project — `npm run test:browser`. The cloud sandbox
// can't launch the browser runner, so this is verified on the dev machine.)
//
// Radios are driven by their VISIBLE LABEL, not by DOM name (Form.io names groups
// data[<key>][<random-per-render suffix>]) and not by `role=radio` either — see
// the note on `option()`. Q1 and Q2 are always the first two Yes/No groups, so
// `option()` picks them by index (0 and 1); every later question is answered
// inside its own radio group with `answer()`, so a question revealed above it
// (the age questions, the spouse questions) cannot shift the target.

const yesNo = [
  { label: "Yes", value: "true" },
  { label: "No", value: "false" },
];
const partneredConditional = {
  json: { in: [{ var: "data.relationshipStatus" }, ["married", "marriagelike"]] },
};
// v5 gates (see form-spec-seed-data.ts). The status question shows only for
// residesInBc = "true", so answering "No" is terminal.
const q1YesConditional = { show: true, when: "residesInBc", eq: "true" };
const hasStatusConditional = { show: true, when: "hasEligibleStatus", eq: "true" };

/** A faithful slice of the v5 estimator spec — real keys + the v5 conditionals. */
const estimatorSpec = {
  formSpecId: "eligibility-estimator",
  version: 5,
  title: "Eligibility Estimator",
  spec: {
    display: "form",
    components: [
      {
        type: "bcgovRadio",
        key: "residesInBc",
        label: "Do you currently reside in British Columbia?",
        input: true,
        values: yesNo,
        dataType: "string",
        validate: { required: true },
      },
      {
        type: "bcgovRadio",
        key: "hasEligibleStatus",
        label: "Do you have a status that allows you to live in Canada?",
        // (No Q2 tooltip — removed from the v3 seed; help lives in the accordion.)
        input: true,
        values: yesNo,
        dataType: "string",
        validate: { required: true },
        conditional: q1YesConditional,
      },
      {
        type: "bcgovAccordion",
        key: "statusHelp",
        input: false,
        accordionLabel:
          'What does "status that allows you to live in Canada" mean?',
        accordionBody:
          "<p>To be eligible for assistance, your status must meet the citizenship and residency requirements.</p>",
        conditional: q1YesConditional,
      },
      {
        type: "bcgovRadio",
        key: "age65",
        label: "Are you 65 years of age or older?",
        input: true,
        values: yesNo,
        validate: { required: true },
        errors: { required: "Please select an option." },
        conditional: hasStatusConditional,
      },
      {
        type: "bcgovRadio",
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
        type: "bcgovRadio",
        key: "partnerAge65",
        label: "Is your spouse 65 years of age or older?",
        input: true,
        values: yesNo,
        conditional: partneredConditional,
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
        type: "bcgovRadio",
        key: "pwd",
        label:
          "Do you plan to apply for the Persons with Disabilities (PWD) designation?",
        input: true,
        values: yesNo,
        validate: { required: true },
        conditional: hasStatusConditional,
      },
      {
        type: "bcgovRadio",
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

// A spec whose spouse age question was deleted in the form editor: couples can
// then never answer it, which is what the page's fallback message is for.
const estimatorSpecWithoutSpouseAge = {
  ...estimatorSpec,
  spec: {
    ...estimatorSpec.spec,
    components: estimatorSpec.spec.components.filter(
      (component) => component.key !== "partnerAge65",
    ),
  },
};

// The spec the stubbed API serves; reset before each test, overridden by the
// tests that need a variant.
let activeSpec: typeof estimatorSpec | typeof estimatorSpecWithRequiredName =
  estimatorSpec;

/** The nine-type rate table (matches the seed). */
const rates = {
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

type Screen = Awaited<ReturnType<typeof renderPage>>;

/**
 * A radio option, located by its visible label.
 *
 * NOT `getByRole("radio")`: these render as the BCDS RadioGroup, which is
 * react-aria, and its real `<input>` sits inside a visually-hidden span within
 * the `<label>`. The role query resolves to an element Playwright refuses to
 * click ("intercepts pointer events"); clicking the label is what a person does
 * and what react-aria listens for.
 *
 * A string is matched EXACTLY (anchored), so "Married" cannot also match
 * "Single and Never Married".
 */
function option(screen: Screen, label: string | RegExp, index = 0) {
  const matcher =
    typeof label === "string"
      ? new RegExp(`^${label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}$`)
      : label;
  return screen.getByText(matcher).nth(index);
}

/** A radio group's accessible name: its label, plus " (required)" when required. */
function groupName(label: string): RegExp {
  const escaped = label.replaceAll(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`);
  return new RegExp(String.raw`^${escaped}( \(required\))?$`);
}

/** Answers a question by clicking an option inside that question's radio group. */
async function answer(screen: Screen, question: string, choice: string) {
  const group = screen.getByRole("radiogroup", { name: groupName(question) });
  await group.getByText(choice, { exact: true }).click();
}

/**
 * Types a number and leaves the field, so the value is committed to the form.
 * Retries because Form.io's validation pass, which runs shortly after each
 * answer, redraws the field and drops text typed but not yet committed.
 */
async function enterNumber(screen: Screen, label: string, value: string) {
  const field = screen.getByRole("textbox", { name: label });
  await expect
    .poll(
      async () => {
        await field.fill(value);
        await userEvent.tab();
        return (field.element() as HTMLInputElement).value;
      },
      { timeout: 10_000 },
    )
    .toBe(value);
}

// The spec references the custom bcgovAccordion type; register it once so
// Form.io renders it instead of a blank slot (mirrors main.tsx at app start).
registerBcgovComponents();

beforeEach(() => {
  activeSpec = estimatorSpec; // clean spec by default
  stubEstimatorApi();
});
afterEach(() => vi.restoreAllMocks());

const Q2_LABEL = "Do you have a status that allows you to live in Canada?";
const RELATIONSHIP_LABEL = "What is your relationship status?";
const AGE_LABEL = "Are you 65 years of age or older?";
const SPOUSE_AGE_LABEL = "Is your spouse 65 years of age or older?";
const PWD_LABEL =
  "Do you plan to apply for the Persons with Disabilities (PWD) designation?";
const SPOUSE_PWD_LABEL =
  "Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?";

// --- Progressive disclosure ------------------------------------------------

test("initially shows only Q1 — Q2 and the remaining questions are hidden", async () => {
  const screen = await renderPage();

  await expect
    .element(screen.getByText("Do you currently reside in British Columbia?"))
    .toBeVisible();
  // Page chrome.
  await expect
    .element(screen.getByText("Your information is private"))
    .toBeVisible();
  await expect.element(screen.getByText("*All fields are required.")).toBeVisible();

  // Nothing past Q1 is present yet.
  await expect.element(screen.getByText(Q2_LABEL)).not.toBeInTheDocument();
  await expect
    .element(screen.getByText(RELATIONSHIP_LABEL))
    .not.toBeInTheDocument();
});

test("answering Q1 reveals Q2 (+ the help accordion) but not the remaining questions", async () => {
  const screen = await renderPage();
  await expect
    .element(screen.getByText("Do you currently reside in British Columbia?"))
    .toBeVisible();

  await option(screen, /^Yes$/, 0).click(); // residesInBc

  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();
  await expect.element(screen.getByText(/What does .* mean\?/)).toBeVisible();
  // Remaining questions stay hidden until Q2 = Yes.
  await expect
    .element(screen.getByText(RELATIONSHIP_LABEL))
    .not.toBeInTheDocument();
});

test("Q2 = Yes reveals the remaining questions and Get Estimate", async () => {
  const screen = await renderPage();
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();

  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes

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
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();

  await option(screen, /^No$/, 1).click(); // hasEligibleStatus = No

  await expect
    .element(screen.getByText("You might not be eligible for assistance"))
    .toBeVisible();
  await expect
    .element(screen.getByText(RELATIONSHIP_LABEL))
    .not.toBeInTheDocument();

  // Both links in the warning box are clickable and point at the real
  // gov.bc.ca pages (residency requirements + hardship "Contact us").
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

test("Q1 = No is terminal — Q2 never reveals and the warning shows", async () => {
  const screen = await renderPage();

  await option(screen, /^No$/, 0).click(); // residesInBc = No

  await expect
    .element(screen.getByText("You might not be eligible for assistance"))
    .toBeVisible();
  // The hard gate: Q2 is not revealed at all, so there is nothing to answer.
  await expect.element(screen.getByText(Q2_LABEL)).not.toBeInTheDocument();
  await expect
    .element(screen.getByText(RELATIONSHIP_LABEL))
    .not.toBeInTheDocument();
});

// --- Announcements ----------------------------------------------------------

test("the not-eligible warning sits in a live region", async () => {
  const screen = await renderPage();

  await option(screen, /^No$/, 0).click(); // residesInBc = No

  const warning = screen.getByText("You might not be eligible for assistance");
  await expect.element(warning).toBeVisible();
  // The wrapper must be mounted before the text appears, or the insertion is
  // announced unreliably — so assert the region, not just the text.
  expect(warning.element().closest("[aria-live]")).not.toBeNull();
});

test("a blocked submit moves focus to the first unanswered question", async () => {
  const screen = await renderPage();

  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes
  await option(screen, /^Yes$/, 1).click(); // Q2 = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();

  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect.element(screen.getByText(/is required/i).first()).toBeVisible();

  // Focus leaves the submit button and lands inside the form, so the error is
  // announced rather than the citizen being left where they clicked.
  await expect
    .poll(() => document.activeElement?.tagName)
    .not.toBe("BUTTON");
  expect(
    document.querySelector("[class*='formHost']")
      ?.contains(document.activeElement),
  ).toBe(true);
});

// --- Validation display -----------------------------------------------------

test("a newly revealed question carries NO error once the form is dirty", async () => {
  const screen = await renderPage();

  // The assertion only means something on a dirty form: before the first
  // submit Form.io passes dirty:false to every component, so the gate in
  // BcgovRadioComponent is never consulted. Submit first to get there.
  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes
  await option(screen, /^Yes$/, 1).click(); // Q2 = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect.element(screen.getByText(/is required/i).first()).toBeVisible();

  // Answer everything outstanding, so no error is left on screen.
  await option(screen, "Single and Never Married").click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");
  await expect
    .element(screen.getByText(/is required/i).first())
    .not.toBeInTheDocument();
  await expect
    .element(screen.getByText("Please select an option."))
    .not.toBeInTheDocument();

  // Hide and re-reveal Q2. It comes back empty and required, on a form Form.io
  // now treats as dirty for the rest of the session.
  await option(screen, /^No$/, 0).click(); // Q1 = No
  await expect.element(screen.getByText(Q2_LABEL)).not.toBeInTheDocument();
  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();

  await expect
    .element(screen.getByText(/is required/i).first())
    .not.toBeInTheDocument();
});

test("re-passing the Q1 gate re-reveals Q2 CLEAN after a blocked submit", async () => {
  const screen = await renderPage();

  // Reach a blocked submit so real errors are on screen.
  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes
  await option(screen, /^Yes$/, 1).click(); // Q2 = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect.element(screen.getByText(/is required/i).first()).toBeVisible();

  // Polling assertion, not a synchronous textContent read: Form.io debounces
  // change propagation, so the hide lands a tick or two after the click.
  await option(screen, /^No$/, 0).click();
  await expect.element(screen.getByText(Q2_LABEL)).not.toBeInTheDocument();

  // ...and re-passing the gate must present a fresh Q2, not one still wearing
  // the error from the earlier attempt.
  await option(screen, /^Yes$/, 0).click();
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();
  await expect
    .element(screen.getByText(/is required/i).first())
    .not.toBeInTheDocument();
});

test("re-passing the Q1 gate re-reveals Q2 CLEAN after a SUCCESSFUL estimate", async () => {
  // The harder case: a *successful* submit leaves Form.io treating the whole
  // form as dirty for the rest of the session, so an empty required field that
  // later becomes visible reports an error immediately.
  const screen = await renderPage();

  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes
  await option(screen, /^Yes$/, 1).click(); // Q2 = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, "Single and Never Married").click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");
  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect
    .element(screen.getByText("You may be eligible for assistance"))
    .toBeVisible();

  // Q1 = No — terminal, everything below hides and clears.
  await option(screen, /^No$/, 0).click();
  await expect.element(screen.getByText(Q2_LABEL)).not.toBeInTheDocument();

  // Back through the gate: Q2 must be clean.
  await option(screen, /^Yes$/, 0).click();
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();
  await expect
    .element(screen.getByText(/is required/i).first())
    .not.toBeInTheDocument();
});

test("errors still appear on a submit attempt after a gate round-trip", async () => {
  const screen = await renderPage();

  // Guards against over-correcting the two tests above: resetting on clear must
  // not permanently suppress validation.
  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes
  await option(screen, /^No$/, 0).click(); // Q1 = No  (clears)
  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes (re-reveal)
  await option(screen, /^Yes$/, 1).click(); // Q2 = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect.element(screen.getByText(/is required/i).first()).toBeVisible();
});

test("Q1 = No, then Yes, then Q2 = Yes reaches a full estimate", async () => {
  const screen = await renderPage();

  await option(screen, /^No$/, 0).click(); // Q1 = No
  await option(screen, /^Yes$/, 0).click(); // Q1 = Yes
  await option(screen, /^Yes$/, 1).click(); // Q2 = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, "Single and Never Married").click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect
    .element(screen.getByText("You may be eligible for assistance"))
    .toBeVisible();
  await expect.element(screen.getByText(/\/ month/)).toBeVisible();
});

test("reveals the spouse section on Married", async () => {
  const screen = await renderPage();
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();

  await option(screen, /^Married$/).click();

  await expect.element(screen.getByText(/Spouse's Monthly Income/)).toBeVisible();
  await expect.element(screen.getByText(SPOUSE_AGE_LABEL)).toBeVisible();
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
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, "Single and Never Married").click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");

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
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, "Single and Never Married").click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");
  // Single type-B income limit is 1060 → 2000 is over the limit → ineligible.
  await enterNumber(screen, "Your Monthly Income", "2000");

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
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, /^Married$/).click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, SPOUSE_AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");

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
  const spousePwd = screen.getByRole("radiogroup", { name: groupName(SPOUSE_PWD_LABEL) });
  await expect.element(spousePwd.getByText("Please select an option.")).toBeVisible();
  expect(document.body.textContent).not.toContain("/ month");
});

test("the same couple gets an estimate once the spouse-PWD question is answered", async () => {
  const screen = await renderPage();
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, /^Married$/).click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, SPOUSE_AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");

  await expect
    .element(
      screen.getByText(
        /Does your spouse plan to apply for the Persons with Disabilities/,
      ),
    )
    .toBeVisible();
  await answer(screen, SPOUSE_PWD_LABEL, "No");

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect
    .element(screen.getByText("You may be eligible for assistance"))
    .toBeVisible();
  await expect.element(screen.getByText(/\/ month/)).toBeVisible();
});

// --- The age questions ------------------------------------------------------

// The question order itself is pinned against the seed in MyssContent's
// eligibility-estimator-seed.test.ts; these tests cover when the questions show.
test("the age question appears only after Q1 and Q2 are Yes", async () => {
  const screen = await renderPage();
  await expect.element(screen.getByText(AGE_LABEL)).not.toBeInTheDocument();

  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await expect.element(screen.getByText(Q2_LABEL)).toBeVisible();
  await expect.element(screen.getByText(AGE_LABEL)).not.toBeInTheDocument();

  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(AGE_LABEL)).toBeVisible();
});

test("the spouse age question appears only for Married and Marriage-Like", async () => {
  const screen = await renderPage();
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await expect.element(screen.getByText(SPOUSE_AGE_LABEL)).not.toBeInTheDocument();

  // Each step changes the visibility, so each assertion waits for that change.
  await option(screen, "Married").click();
  await expect.element(screen.getByText(SPOUSE_AGE_LABEL)).toBeVisible();

  await option(screen, "Single and Never Married").click();
  await expect.element(screen.getByText(SPOUSE_AGE_LABEL)).not.toBeInTheDocument();

  await option(screen, "Marriage-Like Relationship").click();
  await expect.element(screen.getByText(SPOUSE_AGE_LABEL)).toBeVisible();

  await option(screen, "Divorced").click();
  await expect.element(screen.getByText(SPOUSE_AGE_LABEL)).not.toBeInTheDocument();
});

interface AgeCase {
  household: string;
  age: string;
  pwd: string;
  /** Present for a couple. */
  spouse?: { relationship: string; age: string; pwd: string };
  amount: RegExp;
}

// Zero income, no children: the amount is the client type's limit at family size 1 or 2.
test.each<AgeCase>([
  { household: "single 65+ (type E)", age: "Yes", pwd: "No", amount: /\$1,360\.00/ },
  { household: "single PWD and 65+ (type G)", age: "Yes", pwd: "Yes", amount: /\$1,535\.50/ },
  { household: "couple, both 65+ (type C)", age: "Yes", pwd: "No", spouse: { relationship: "Married", age: "Yes", pwd: "No" }, amount: /\$2,200\.00/ },
  { household: "couple, one 65+ (type D)", age: "No", pwd: "No", spouse: { relationship: "Married", age: "Yes", pwd: "No" }, amount: /\$1,950\.00/ },
  { household: "marriage-like couple, one 65+ (type D)", age: "Yes", pwd: "No", spouse: { relationship: "Marriage-Like Relationship", age: "No", pwd: "No" }, amount: /\$1,950\.00/ },
  { household: "couple, applicant PWD and spouse 65+ (type I)", age: "No", pwd: "Yes", spouse: { relationship: "Married", age: "Yes", pwd: "No" }, amount: /\$2,590\.50/ },
  { household: "couple, applicant PWD and 65+, spouse neither (type F)", age: "Yes", pwd: "Yes", spouse: { relationship: "Married", age: "No", pwd: "No" }, amount: /\$2,290\.50/ },
])("estimates $household", async ({ age, pwd, spouse, amount }) => {
  const screen = await renderPage();
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();

  await answer(screen, AGE_LABEL, age);
  await option(screen, spouse?.relationship ?? "Single and Never Married").click();
  if (spouse) {
    await answer(screen, SPOUSE_AGE_LABEL, spouse.age);
    await answer(screen, SPOUSE_PWD_LABEL, spouse.pwd);
  }
  await answer(screen, PWD_LABEL, pwd);

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect.element(screen.getByText(amount)).toBeVisible();
});

test("a couple who leaves the spouse age question blank is blocked, not silently scored as 'No'", async () => {
  const screen = await renderPage();
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await answer(screen, AGE_LABEL, "No");
  await option(screen, /^Married$/).click();
  await answer(screen, PWD_LABEL, "No");
  await answer(screen, SPOUSE_PWD_LABEL, "No");

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  // Required at runtime like the spouse PWD question: an inline error on the
  // spouse age question, and no estimate.
  const spouseAge = screen.getByRole("radiogroup", { name: groupName(SPOUSE_AGE_LABEL) });
  await expect.element(spouseAge.getByText("Please select an option.")).toBeVisible();
  expect(document.body.textContent).not.toContain("/ month");
});

test("a couple is asked to answer the spouse questions when one is missing from the form", async () => {
  activeSpec = estimatorSpecWithoutSpouseAge;
  const screen = await renderPage();
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await answer(screen, AGE_LABEL, "No");
  await option(screen, /^Married$/).click();
  await answer(screen, PWD_LABEL, "No");
  await answer(screen, SPOUSE_PWD_LABEL, "No");

  await screen.getByRole("button", { name: "Get Estimate" }).click();

  await expect
    .element(screen.getByRole("alert"))
    .toHaveTextContent("Please answer the questions about your spouse.");
  expect(document.body.textContent).not.toContain("/ month");
});

// --- Regression: the result only changes on a "Get Estimate" click ---------

/** Married + both PWD, no income → family size 2, column h = $2,766.00. */
async function reachCoupleEstimate(screen: Awaited<ReturnType<typeof renderPage>>) {
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, /^Married$/).click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, SPOUSE_AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "Yes");
  await expect
    .element(
      screen.getByText(
        /Does your spouse plan to apply for the Persons with Disabilities/,
      ),
    )
    .toBeVisible();
  await answer(screen, SPOUSE_PWD_LABEL, "Yes");
  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect.element(screen.getByText(/\$2,766\.00/)).toBeVisible();
}

// A plain field edit must NOT flicker or clear the shown result — the estimate
// only refreshes when the user clicks Get Estimate again.
test("editing a field after an estimate leaves the result on screen", async () => {
  const screen = await renderPage();
  await reachCoupleEstimate(screen);

  await screen.getByRole("textbox", { name: "Spouse's Monthly Income" }).fill("50");

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
  await option(screen, /^Yes$/, 0).click(); // residesInBc = Yes
  await option(screen, /^Yes$/, 1).click(); // hasEligibleStatus = Yes
  await expect.element(screen.getByText(RELATIONSHIP_LABEL)).toBeVisible();
  await option(screen, "Single and Never Married").click();
  await answer(screen, AGE_LABEL, "No");
  await answer(screen, PWD_LABEL, "No");
  await screen.getByRole("button", { name: "Get Estimate" }).click();
  await expect.element(screen.getByText(/\$1,060\.00/)).toBeVisible();

  // Flip Q2 to No → the warning appears and the stale estimate is cleared.
  await option(screen, /^No$/, 1).click(); // hasEligibleStatus = No

  await expect
    .element(screen.getByText("You might not be eligible for assistance"))
    .toBeVisible();
  // Async, retried — the clear runs in the change handler as the warning
  // renders, so a synchronous body check here could race the re-render.
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
