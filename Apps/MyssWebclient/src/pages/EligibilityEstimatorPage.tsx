import { Form } from "@formio/react";
import { useEffect, useRef, useState } from "react";
import { InlineAlert } from "@bcgov/design-system-react-components";

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

// --- Programme content (URLs confirmed; some copy still placeholder — see plan §8) ---
// TODO(content): remaining real copy pending from the programme / content designer.
const PENDING = {
  // "current income assistance rates" reference → gov.bc.ca IA rate table.
  ratesInfoUrl:
    "https://www2.gov.bc.ca/gov/content/governments/policies-for-government/bcea-policy-and-procedure-manual/bc-employment-and-assistance-rate-tables/income-assistance-rate-table",
  // "Contact us…" hardship-assistance link (shown on a $0 / ineligible result).
  hardshipUrl:
    "https://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/access-services",
  // "residency requirements" link inside the Q2="No" warning (0901 ee-05) →
  // same citizenship-requirements page as the status-help accordion.
  residenceReqUrl:
    "https://www2.gov.bc.ca/gov/content/governments/policies-for-government/bcea-policy-and-procedure-manual/eligibility/citizenship-requirements",
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
  // Latest live form answers, used to show the inline "not eligible" warning the
  // moment Q2 is answered "No" — before submit. Read from the Form.io instance
  // (onChange's payload is (value, flags, modified), not a { data } submission).
  const [liveAnswers, setLiveAnswers] = useState<Record<string, unknown>>({});
  const formInstanceRef = useRef<{ data?: unknown } | null>(null);
  const resultHeadingRef = useRef<HTMLHeadingElement>(null);

  // Move focus to the result when it appears, so a screen-reader user is told
  // the estimate is ready rather than being left at the submit button.
  useEffect(() => {
    if (outcome) resultHeadingRef.current?.focus();
  }, [outcome]);

  // Design 0901 ee-05: this page reads white edge-to-edge. The app shell paints
  // an off-white ground on #root (App.css), which shows through the whole
  // viewport. Override it to white while the estimator is mounted and restore it
  // on leave — scoped to this route only, and nothing inside the estimator's own
  // markup is touched (the privacy banner and Q2="No" warning keep their own
  // backgrounds).
  useEffect(() => {
    const root = document.getElementById("root");
    if (!root) return;
    const previous = root.style.backgroundColor;
    root.style.backgroundColor = "#ffffff";
    return () => {
      root.style.backgroundColor = previous;
    };
  }, []);

  // Form.io fires onChange on every edit. Its first arg may carry `.data`, but
  // the instance's current `.data` is the reliable source; spread into a new
  // object so React re-renders. (v3 gates the rest of the form + submit on
  // Q2=Yes, so when Q2=No only Q1/Q2 are visible and the warning sits below.)
  function handleFormReady(instance: {
    data?: unknown;
    getComponent?: (
      key: string,
    ) =>
      | {
          component?: {
            validate?: Record<string, unknown>;
            errors?: Record<string, unknown>;
          };
        }
      | undefined;
  }) {
    formInstanceRef.current = instance;
    // `partnerPwd` cannot be server-`required` (an advanced-conditional required
    // field fails the FormSpecValidator and would reject singles — Decision B).
    // Mark it required at RUNTIME, with a matching message, so Form.io renders the
    // SAME inline error as the applicant PWD field. It is shown only for couples,
    // so singles — where it stays hidden — are never validated. The couple-check
    // in handleSubmit remains as a fallback.
    const partner = instance.getComponent?.("partnerPwd");
    if (partner?.component) {
      partner.component.validate = {
        ...(partner.component.validate ?? {}),
        required: true,
      };
      partner.component.errors = {
        ...(partner.component.errors ?? {}),
        required: "Please select an option.",
      };
    }
  }

  function handleChange(value?: { data?: unknown }) {
    const data = (value?.data ?? formInstanceRef.current?.data) as
      | Record<string, unknown>
      | undefined;
    if (data) setLiveAnswers({ ...data });
  }

  // Hide any previously shown estimate so a stale result card can't linger over
  // an errored form. Only clears when there is something to clear (functional
  // updater keeps a no-op cheap — no re-render when there's no result).
  function clearStaleResult() {
    setOutcome((prev) => (prev ? null : prev));
  }

  // Q2 ("status that allows you to live in Canada") answered "No". The v3 seed
  // pins dataType "string" so this is "false", but accept the boolean form too
  // in case a spec is served without it. Copy is hardcoded for this increment;
  // the §5 content pass will source it from estimator-content.
  const showStatusWarning =
    liveAnswers.hasEligibleStatus === "false" ||
    liveAnswers.hasEligibleStatus === false;

  // If the answers move into the Q2 = "No" screen-fail state (the inline "might
  // not be eligible" warning), a previously shown estimate now contradicts the
  // form — clear it so a stale result card can't sit under the warning. A plain
  // field edit still leaves the result untouched; only this eligibility-gating
  // change clears it.
  useEffect(() => {
    if (showStatusWarning) setOutcome((prev) => (prev ? null : prev));
  }, [showStatusWarning]);

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
          <Form
            src={spec.data.spec}
            // MYSS 0903 (screen 08): design is inline-only, so suppress Form.io's
            // aggregated `.alert-danger` summary banner. Per-field errors remain.
            // NOTE: a11y follow-up — move focus to the first invalid field on a
            // blocked submit to replace the summary's jump links.
            options={{ noAlerts: true }}
            onSubmit={handleSubmit}
            onChange={handleChange}
            onFormReady={handleFormReady}
            // Form.io v5 short-circuits a submit blocked by validation: it emits
            // NO form-level `submit`/`submitError`/`error`, only a per-field
            // `componentError`. So `onSubmit` never runs to refresh the result,
            // leaving a stale estimate over the errored form. Clearing on
            // `componentError` hides it. A VALID submit emits no componentError,
            // so a good estimate is never flickered away.
            otherEvents={{ "formio.componentError": clearStaleResult }}
          />
        </div>
      )}

      {/* Inline, progressive "not eligible" warning (0901 ee-02/ee-05). Shows
          the moment Q2 is answered "No"; the v3 seed keeps the rest of the form
          hidden in that state, so this sits directly under the visible questions. */}
      {showStatusWarning && (
        <div className={styles.statusWarning}>
          <InlineAlert variant="warning">
            {/* This InlineAlert renders its `title` prop only when it has no
                children; since we need a rich body, we render the title inside
                children using the component's own title markup (class "title",
                id "alert-title" — the target of the container's aria-labelledby). */}
            <span className="title" id="alert-title">
              You might not be eligible for assistance
            </span>
            <p>
              Based on your answer, you might not meet the{" "}
              <a
                className={styles.inlineLink}
                href={PENDING.residenceReqUrl}
                target="_blank"
                rel="noreferrer"
              >
                residency requirements
              </a>{" "}
              for assistance in British Columbia.
            </p>
            <p>
              <strong>Not eligible but still in need?</strong> You may be able to
              receive hardship assistance, depending on your circumstances.{" "}
              <a
                className={styles.inlineLink}
                href={PENDING.hardshipUrl}
                target="_blank"
                rel="noreferrer"
              >
                Contact us to find out more about this kind of support.
              </a>
            </p>
          </InlineAlert>
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
                <a
                  className={styles.inlineLink}
                  href={PENDING.hardshipUrl}
                  target="_blank"
                  rel="noreferrer"
                >
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
                <a
                  className={styles.inlineLink}
                  href={PENDING.hardshipUrl}
                  target="_blank"
                  rel="noreferrer"
                >
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
