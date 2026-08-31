import { Form } from "@formio/react";
import { useEffect, useRef, useState } from "react";

import "@formio/js/dist/formio.form.min.css";

import {
  calculateEstimate,
  mapAnswersToEstimate,
  missingRequiredCoupleAnswers,
  screenPreCheck,
  useEstimatorRates,
  useEstimatorSpec,
  type EligibilityResult,
} from "@/hooks/useEligibility";
import styles from "./EligibilityEstimatorPage.module.css";

// The public, anonymous Pre-Eligibility Estimator (MYSS-169, Option B). It
// renders the Form.io spec served by MyssApi, hard-screens on the residency /
// status pre-check, and computes the estimate CLIENT-SIDE against the fetched
// rate table (no server calculation, nothing persisted). Result UI follows the
// 0826 design: an estimate card + prose only — NO itemised breakdown table
// (Decision A). The spouse section (incl. partnerPwd) is revealed by the seed's
// own Form.io conditional on married / marriage-like (Decision B).

// --- Pending programme content (clearly-marked placeholders; see plan §8) ---
// TODO(content): real copy + URLs pending from the programme / content designer.
const PENDING = {
  // Link target for the "current income assistance rates" reference.
  ratesInfoUrl: "#",
  // "Contact us…" hardship-assistance link (shown on a $0 / ineligible result).
  hardshipUrl: "#",
  // Copy for a residency / status pre-check "No" (no artboard exists for this yet).
  preCheckFailLede:
    "Based on your answers, you may not be eligible for assistance from this ministry.",
  preCheckFailBody:
    "To receive assistance you must live in British Columbia and have a status that allows you to live in Canada.",
} as const;

type Outcome =
  | {
      kind: "estimate";
      result: EligibilityResult;
      answers: Record<string, unknown>;
    }
  | { kind: "prescreen" }
  | { kind: "incomplete" };

// Card amount keeps cents ($1,060.00); the "Your information" echo is whole
// dollars ($500), matching the 0826 frames.
const moneyCents = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});
const moneyWhole = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  maximumFractionDigits: 0,
});

/**
 * A friendly "Household type" label for the result echo. Placeholder taxonomy
 * pending the content designer: couple → Married / Marriage-like; a lone adult
 * with dependants → "Single parent" (as the 0826 frame shows), else "Single".
 */
function householdTypeLabel(answers: Record<string, unknown>): string {
  const status = String(answers.relationshipStatus ?? "");
  if (status === "married") return "Married";
  if (status === "marriagelike") return "Marriage-like";
  const deps = Number(answers.dependentChildren ?? 0);
  return Number.isFinite(deps) && deps > 0 ? "Single parent" : "Single";
}

/** The "current income assistance rates" inline reference link (reused). */
function RatesLink() {
  return (
    <a
      className={styles.inlineLink}
      href={PENDING.ratesInfoUrl}
      target="_blank"
      rel="noreferrer"
    >
      current income assistance rates
    </a>
  );
}

/** The "Your information" echo shown under both eligible and ineligible results. */
function YourInformation({
  result,
  answers,
}: {
  result: EligibilityResult;
  answers: Record<string, unknown>;
}) {
  return (
    <div className={styles.yourInfo}>
      <h3 className={styles.subHeading}>Your information</h3>
      <dl className={styles.infoList}>
        <div className={styles.infoRow}>
          <dt>Family size</dt>
          <dd>
            {result.familySize}
            {result.familySizeClamped ? " (capped at 7)" : ""}
          </dd>
        </div>
        <div className={styles.infoRow}>
          <dt>Household type</dt>
          <dd>{householdTypeLabel(answers)}</dd>
        </div>
        <div className={styles.infoRow}>
          <dt>Monthly income</dt>
          <dd>{moneyWhole.format(result.monthlyIncome)}</dd>
        </div>
        <div className={styles.infoRow}>
          <dt>Assets</dt>
          <dd>{moneyWhole.format(result.totalAssets)}</dd>
        </div>
      </dl>
    </div>
  );
}

export default function EligibilityEstimatorPage() {
  const spec = useEstimatorSpec();
  const rates = useEstimatorRates();
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const resultHeadingRef = useRef<HTMLHeadingElement>(null);

  // Move focus to the result when it appears, so a screen-reader user is told
  // the estimate is ready rather than being left at the submit button.
  useEffect(() => {
    if (outcome) resultHeadingRef.current?.focus();
  }, [outcome]);

  function handleSubmit(submission: { data: Record<string, unknown> }) {
    const answers = submission.data;

    // Residency / status is a hard eligibility screen — a "No" short-circuits
    // WITHOUT running the calculation.
    if (!screenPreCheck(answers).passed) {
      setOutcome({ kind: "prescreen" });
      return;
    }

    // partnerPwd is a yes/no radio that carries no server-side `required` (it
    // would break single applicants — see missingRequiredCoupleAnswers). An
    // unanswered spouse-disability question must NOT be silently scored as "No",
    // so refuse to compute until a couple has answered it.
    if (missingRequiredCoupleAnswers(answers).length > 0) {
      setOutcome({ kind: "incomplete" });
      return;
    }

    // The form is only interactable once rates have loaded (guarded below), so
    // rates.data is present here; the check keeps TypeScript honest.
    if (!rates.data) return;

    const result = calculateEstimate(mapAnswersToEstimate(answers), rates.data);
    setOutcome({ kind: "estimate", result, answers });
  }

  const loading = spec.isPending || rates.isPending;
  const loadError = spec.error || rates.error;

  return (
    <div className={styles.page}>
      <h1 className={styles.title}>Estimate your Eligibility for Assistance</h1>

      <aside className={styles.privacyBanner}>
        <p className={styles.privacyTitle}>Your information is private</p>
        <p className={styles.privacyBody}>
          None of the information you share is collected or saved.
        </p>
      </aside>

      <p className={styles.requiredNote}>*All fields are required.</p>

      {loading && <p className={styles.loading}>Loading the estimator…</p>}

      {!loading && loadError && (
        <p role="alert" className={styles.error}>
          The estimator could not be loaded right now. Please try again later.
        </p>
      )}

      {!loading && !loadError && spec.data && (
        <div className={styles.formHost}>
          {/* Anonymous render of the served spec — not the old hardcoded components. */}
          <Form src={spec.data.spec} onSubmit={handleSubmit} />
        </div>
      )}

      {outcome?.kind === "incomplete" && (
        <p role="alert" className={styles.error}>
          Please answer whether your spouse plans to apply for the Persons with
          Disabilities (PWD) designation. We need this to estimate your
          eligibility.
        </p>
      )}

      {outcome && outcome.kind !== "incomplete" && (
        <section className={styles.result} aria-live="polite">
          <h2 className={styles.resultTitle} tabIndex={-1} ref={resultHeadingRef}>
            Your eligibility estimate
          </h2>

          {outcome.kind === "prescreen" ? (
            <>
              <div className={styles.estimateCard}>
                <h3 className={styles.estimateHeading}>
                  You may not be eligible for assistance
                </h3>
                <p className={styles.estimateLede}>{PENDING.preCheckFailLede}</p>
              </div>

              <h2 className={styles.subHeading}>Not eligible but still in need?</h2>
              <p className={styles.prose}>{PENDING.preCheckFailBody}</p>
              <p className={styles.prose}>
                You may be able to receive hardship assistance, depending on your
                circumstances.{" "}
                <a className={styles.inlineLink} href={PENDING.hardshipUrl}>
                  Contact us to find out more about this kind of support.
                </a>
              </p>
            </>
          ) : outcome.result.eligible ? (
            <>
              <div className={styles.estimateCard}>
                <h3 className={styles.estimateHeading}>
                  You may be eligible for assistance
                </h3>
                <p className={styles.estimateLede}>
                  Based on the information you provided, the estimated amount is:
                </p>
                <p className={styles.estimateAmount}>
                  {moneyCents.format(outcome.result.estimatedAmount)}{" "}
                  <span className={styles.perMonth}>/ month</span>
                </p>
                <p className={styles.estimateCaveat}>
                  This is only an estimate. The actual amount may be different.
                </p>
              </div>

              <h2 className={styles.subHeading}>How your estimate was calculated</h2>
              <p className={styles.prose}>
                The estimated amount is based on your household information and the{" "}
                <RatesLink /> for support and shelter allowance. The estimate is
                showing the maximum amount you could receive.
              </p>

              <YourInformation
                result={outcome.result}
                answers={outcome.answers}
              />
            </>
          ) : (
            <>
              <div className={styles.estimateCard}>
                <h3 className={styles.estimateHeading}>
                  You may not be eligible for assistance
                </h3>
                <p className={styles.estimateLede}>
                  Based on the information you provided, the estimated amount is:
                </p>
                <p className={styles.estimateAmount}>
                  {moneyWhole.format(0)}{" "}
                  <span className={styles.perMonth}>/ month</span>
                </p>
                <p className={styles.estimateCaveat}>
                  This is only an estimate. The actual amount may be different.
                </p>
              </div>

              <h2 className={styles.subHeading}>Not eligible but still in need?</h2>
              <p className={styles.prose}>
                You may be able to receive hardship assistance, depending on your
                circumstances.{" "}
                <a className={styles.inlineLink} href={PENDING.hardshipUrl}>
                  Contact us to find out more about this kind of support.
                </a>
              </p>

              <h2 className={styles.subHeading}>Why is my estimate $0?</h2>
              <p className={styles.prose}>
                The estimated amount is based on your household information and the{" "}
                <RatesLink /> for support and shelter allowance. Based on the
                information you provided, your estimated monthly assistance amount
                is $0.
              </p>

              <YourInformation
                result={outcome.result}
                answers={outcome.answers}
              />
            </>
          )}
        </section>
      )}
    </div>
  );
}
