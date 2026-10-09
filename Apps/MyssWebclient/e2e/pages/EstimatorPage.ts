import { expect, type Locator, type Page } from "@playwright/test";

// The questions as the seeded spec labels them. Steps speak in these labels so
// a feature reads like the form, and a renamed question fails in one place.
export const Question = {
  residesInBc: "Do you currently reside in British Columbia?",
  hasEligibleStatus: "Do you have a status that allows you to live in Canada?",
  relationshipStatus: "What is your relationship status?",
  dependentChildren:
    "How many dependent children under the age of 19 live with you?",
  pwd: "Do you plan to apply for the Persons with Disabilities (PWD) designation?",
  partnerPwd:
    "Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?",
  monthlyIncome: "Your Monthly Income",
  partnerMonthlyIncome: "Spouse's Monthly Income",
  assetValue:
    "What is the total value of your assets not listed above (property, investments, cash or savings)?",
} as const;

/**
 * A field's accessible name is its label, which Form.io suffixes with
 * " (required)" on required fields. Match the whole name either way, so
 * "Married" can never also match "Single and Never Married".
 */
function fieldName(label: string): RegExp {
  const escaped = label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return new RegExp(`^${escaped}( \\(required\\))?$`);
}

/** The public estimator page, driven the way a person drives it. */
export class EstimatorPage {
  private readonly page: Page;

  constructor(page: Page) {
    this.page = page;
  }

  async open(): Promise<void> {
    await this.page.goto("/eligibility-estimator");
    await expect(this.question(Question.residesInBc)).toBeVisible();
  }

  /**
   * A choice question: the BC Design System radio group, named by its label.
   * (The custom bcgovRadio component wires `aria-labelledby` to the label.)
   */
  question(label: string): Locator {
    return this.page.getByRole("radiogroup", { name: fieldName(label) });
  }

  /**
   * Pick an option by clicking its visible label, as a person does. The real
   * radio input is visually hidden inside the label, so the role query is not
   * clickable; the label is what react-aria listens for.
   */
  async answer(label: string, option: string): Promise<void> {
    await this.question(label).getByText(option, { exact: true }).click();
  }

  /** A number question: Form.io renders a labelled text input. */
  async enter(label: string, value: number): Promise<void> {
    await this.page
      .getByRole("textbox", { name: fieldName(label) })
      .fill(String(value));
  }

  async askForEstimate(): Promise<void> {
    await this.page.getByRole("button", { name: "Get Estimate" }).click();
  }

  /** The inline "might not be eligible" warning shown on a "No". */
  warning(): Locator {
    return this.page.getByText("You might not be eligible for assistance");
  }

  residencyRequirementsLink(): Locator {
    return this.page.getByRole("link", { name: "residency requirements" });
  }

  eligibleHeading(): Locator {
    return this.page.getByRole("heading", {
      name: "You may be eligible for assistance",
    });
  }

  notEligibleHeading(): Locator {
    return this.page.getByRole("heading", {
      name: "You may not be eligible for assistance",
    });
  }

  whyZeroHeading(): Locator {
    return this.page.getByRole("heading", { name: "Why is my estimate $0?" });
  }

  /** The "$1,060.00 / month" line on the estimate card. */
  amount(): Locator {
    return this.page.getByText(/^\$[\d,]+(\.\d{2})? \/ month$/);
  }

  /** One row of the "Your information" echo, by its term. */
  info(term: string): Locator {
    return this.page.locator(`dt:text-is("${term}") + dd`);
  }

  /** Form.io's per-field required message, whichever wording the spec uses. */
  requiredError(): Locator {
    return this.page.getByText(/is required|please select an option/i).first();
  }

  /** Whether keyboard focus is inside the rendered form. */
  focusIsInForm(): Promise<boolean> {
    return this.page.evaluate(
      () =>
        document.activeElement?.closest("[class*='formHost']") !== null &&
        document.activeElement !== document.body,
    );
  }
}
