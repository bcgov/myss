import { expect } from "@playwright/test";

import { Given, Then, When } from "../fixtures";
import { Question } from "../pages/EstimatorPage";
import { stubEstimatorApi } from "../support/estimatorApi";

// --- Getting there ---------------------------------------------------------

Given(
  "the estimator serves the seeded form and the August 2023 rates",
  async ({ page }) => {
    await stubEstimatorApi(page);
  },
);

Given("I open the eligibility estimator", async ({ estimator }) => {
  await estimator.open();
});

// --- Questions -------------------------------------------------------------

Then("I see the question {string}", async ({ estimator }, label: string) => {
  await expect(estimator.question(label)).toBeVisible();
});

Then(
  "I do not see the question {string}",
  async ({ estimator }, label: string) => {
    await expect(estimator.question(label)).toHaveCount(0);
  },
);

When(
  "I answer {string} with {string}",
  async ({ estimator }, label: string, option: string) => {
    await estimator.answer(label, option);
  },
);

Given("I live in BC with an eligible status", async ({ estimator }) => {
  await estimator.answer(Question.residesInBc, "Yes");
  await estimator.answer(Question.hasEligibleStatus, "Yes");
});

Given(
  "I am {string} with {int} dependent children",
  async ({ estimator }, status: string, dependants: number) => {
    await estimator.answer(Question.relationshipStatus, status);
    await estimator.enter(Question.dependentChildren, dependants);
  },
);

Given(
  /^I (plan|do not plan) to apply for the PWD designation$/,
  async ({ estimator }, plan: string) => {
    await estimator.answer(Question.pwd, plan === "plan" ? "Yes" : "No");
  },
);

Given(
  /^my spouse (plans|does not plan) to apply for the PWD designation$/,
  async ({ estimator }, plans: string) => {
    await estimator.answer(
      Question.partnerPwd,
      plans === "plans" ? "Yes" : "No",
    );
  },
);

Given(
  "my monthly income is ${float}",
  async ({ estimator }, amount: number) => {
    await estimator.enter(Question.monthlyIncome, amount);
  },
);

Given(
  "my spouse's monthly income is ${float}",
  async ({ estimator }, amount: number) => {
    await estimator.enter(Question.partnerMonthlyIncome, amount);
  },
);

Given(
  "my other assets are worth ${float}",
  async ({ estimator }, value: number) => {
    await estimator.enter(Question.assetValue, value);
  },
);

When("I ask for an estimate", async ({ estimator }) => {
  await estimator.askForEstimate();
});

// --- Outcomes --------------------------------------------------------------

Then("I am warned that I might not be eligible", async ({ estimator }) => {
  await expect(estimator.warning()).toBeVisible();
});

Then(
  "the warning links to the residency requirements",
  async ({ estimator }) => {
    const link = estimator.residencyRequirementsLink();
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute("href", /citizenship-requirements/);
  },
);

Then(
  "I may be eligible for {string} per month",
  async ({ estimator }, amount: string) => {
    await expect(estimator.eligibleHeading()).toBeVisible();
    await expect(estimator.amount()).toHaveText(`${amount} / month`);
  },
);

Then("I may not be eligible, with an estimate of $0", async ({ estimator }) => {
  await expect(estimator.notEligibleHeading()).toBeVisible();
  await expect(estimator.amount()).toHaveText("$0 / month");
});

Then("I am told why my estimate is $0", async ({ estimator }) => {
  await expect(estimator.whyZeroHeading()).toBeVisible();
});

Then(
  "my information shows a family size of {int} and a {string} household",
  async ({ estimator }, size: number, household: string) => {
    await expect(estimator.info("Family size")).toHaveText(String(size));
    await expect(estimator.info("Household type")).toHaveText(household);
  },
);

Then(
  "my information shows a monthly income of {string} and assets of {string}",
  async ({ estimator }, income: string, assets: string) => {
    await expect(estimator.info("Monthly income")).toHaveText(income);
    await expect(estimator.info("Assets")).toHaveText(assets);
  },
);

Then(
  "I am asked to answer the questions marked as required",
  async ({ estimator }) => {
    await expect(estimator.requiredError()).toBeVisible();
  },
);

Then("focus has moved into the form", async ({ estimator }) => {
  await expect.poll(() => estimator.focusIsInForm()).toBe(true);
});
