import { Form } from "@formio/react";
import { useEffect, useRef, useState } from "react";
import {
  Callout,
  InlineAlert,
  SvgExclamationCircleIcon,
} from "@bcgov/design-system-react-components";

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

// The public, anonymous Pre-Eligibility Estimator. It renders the Form.io spec
// served by MyssApi, hard-screens on the residency / status pre-check, and computes
// the estimate CLIENT-SIDE against the fetched rate table (no server calculation,
// nothing persisted). The result is an estimate card + prose only, with no
// itemised breakdown table. The spouse section (incl. partnerPwd) is revealed by
// the seed's own Form.io conditional on married / marriage-like.

// --- Programme content (URLs confirmed; some copy still placeholder) ---
// TODO(content): remaining real copy pending from the programme / content designer.
const PENDING = {
  // "current income assistance rates" reference → gov.bc.ca IA rate table.
  ratesInfoUrl:
    "https://www2.gov.bc.ca/gov/content/governments/policies-for-government/bcea-policy-and-procedure-manual/bc-employment-and-assistance-rate-tables/income-assistance-rate-table",
  // "Contact us…" hardship-assistance link (shown on a $0 / ineligible result).
  hardshipUrl:
    "https://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/access-services",
  // "residency requirements" link inside the "not eligible" warning — the same
  // citizenship-requirements page as the status-help accordion.
  residenceReqUrl:
    "https://www2.gov.bc.ca/gov/content/governments/policies-for-government/bcea-policy-and-procedure-manual/eligibility/citizenship-requirements",
  // Copy for a residency / status pre-check "No" (no artboard exists for this yet).
  preCheckFailLede:
    "Based on your answers, you may not be eligible for assistance from this ministry.",
  preCheckFailBody:
    "To receive assistance you must live in British Columbia and have a status that allows you to live in Canada.",
} as const;

// Announced when a submit is blocked and no invalid field could take focus.
const BLOCKED_SUBMIT_MESSAGE =
  "There is a problem. Please answer the questions marked with an error, then select Get Estimate again.";

type Outcome =
  | {
      kind: "estimate";
      result: EligibilityResult;
      answers: Record<string, unknown>;
    }
  | { kind: "prescreen" }
  | { kind: "incomplete" };

// Card amount keeps cents ($1,060.00); the "Your information" echo is whole
// dollars ($500), matching the design.
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
 * The "might not be eligible" state: either question answered "No". Both are
 * terminal in the spec — a "No" to the first never reveals the second — so the
 * warning has to fire off whichever was answered. The seed pins
 * `dataType: "string"`, but the boolean form is accepted too. Mirrors
 * `screenPreCheck`, the submit-time gate.
 */
function isScreenFail(answers: Record<string, unknown>): boolean {
  const isNo = (value: unknown) => value === "false" || value === false;
  return isNo(answers.residesInBc) || isNo(answers.hasEligibleStatus);
}

/**
 * A friendly "Household type" label for the result echo. Placeholder taxonomy
 * pending the content designer: couple → Married / Marriage-like; a lone adult
 * with dependants → "Single parent", else "Single".
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
      <h3 className={styles.infoHeading}>Your information</h3>
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
  // Announcement for a submit Form.io blocked. Empty whenever focus could be
  // moved to the offending field instead — see handleBlockedSubmit.
  const [blockedMessage, setBlockedMessage] = useState("");
  const formInstanceRef = useRef<{ data?: unknown } | null>(null);
  const resultHeadingRef = useRef<HTMLHeadingElement>(null);
  const formHostRef = useRef<HTMLDivElement>(null);
  const blockedSubmitTimer = useRef<number | null>(null);

  useEffect(
    () => () => {
      if (blockedSubmitTimer.current !== null) {
        window.clearTimeout(blockedSubmitTimer.current);
      }
    },
    [],
  );

  // Move focus to the result when it appears, so a screen-reader user is told
  // the estimate is ready rather than being left at the submit button.
  useEffect(() => {
    if (outcome) resultHeadingRef.current?.focus();
  }, [outcome]);

  // This page reads white edge-to-edge. The app shell paints
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
  // object so React re-renders. (The spec gates the rest of the form and the
  // submit button, so on a "No" only the first questions show and the warning
  // sits below them.)
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
    // `partnerPwd` cannot be server-`required` — an advanced-conditional required
    // field fails the FormSpecValidator and would reject singles.
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
    if (!data) return;
    // Any edit supersedes the previous blocked-submit announcement.
    setBlockedMessage("");
    setLiveAnswers({ ...data });
    // An edit into the screen-fail state contradicts any estimate already shown,
    // so clear it. Done here rather than in an effect on the derived state:
    // setState inside an effect cascades renders.
    if (isScreenFail(data)) clearStaleResult();
  }

  // Hide any previously shown estimate so a stale result card can't linger over
  // an errored form. Only clears when there is something to clear (functional
  // updater keeps a no-op cheap — no re-render when there's no result).
  function clearStaleResult() {
    setOutcome((prev) => (prev ? null : prev));
  }

  /**
   * Send focus to the first field Form.io marked invalid. Returns false when
   * there is nothing to focus, which is the signal to announce instead.
   */
  function focusFirstInvalidField(): boolean {
    const host = formHostRef.current;
    if (!host) return false;
    const invalid = host.querySelector<HTMLElement>(
      '.formio-error-wrapper input, [aria-invalid="true"], [data-invalid]',
    );
    if (!invalid) return false;
    // A radio group is not focusable itself; its first option is.
    const target = invalid.matches("input, select, textarea, button")
      ? invalid
      : (invalid.querySelector<HTMLElement>("input, select, textarea") ??
        invalid);
    target.focus();
    return document.activeElement === target;
  }

  // Form.io emits no form-level event for a blocked submit, only a per-field
  // componentError, so nothing else can report one. It fires once PER invalid
  // field, hence the timer: wait for them all before picking a target.
  function handleBlockedSubmit() {
    clearStaleResult();
    if (blockedSubmitTimer.current !== null) return;
    blockedSubmitTimer.current = window.setTimeout(() => {
      blockedSubmitTimer.current = null;
      // A focused field announces its own label and error, so announcing as
      // well would say the same thing twice. The message is the fallback for
      // when no field could take focus.
      setBlockedMessage(focusFirstInvalidField() ? "" : BLOCKED_SUBMIT_MESSAGE);
    }, 0);
  }

  // Copy is hardcoded for now; a later content pass will source it from
  // estimator-content.
  const showStatusWarning = isScreenFail(liveAnswers);

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

      {/* The real BCDS Callout. Its accent bar, surface, radius and type scale all
          come from the component, so none of it can drift from the design system the
          way a hand-rolled copy did. */}
      <div className={styles.privacyCallout}>
        <Callout
          variant="lightGrey"
          title="Your information is private"
          description="None of the information you share is collected or saved."
        />
      </div>

      <p className={styles.requiredNote}>*All fields are required.</p>

      {loading && <p className={styles.loading}>Loading the estimator…</p>}

      {!loading && loadError && (
        <p role="alert" className={styles.error}>
          The estimator could not be loaded right now. Please try again later.
        </p>
      )}

      {!loading && !loadError && spec.data && (
        <div className={styles.formHost} ref={formHostRef}>
          {/* Anonymous render of the served spec — not the old hardcoded components. */}
          <Form
            src={spec.data.spec}
            // The designed error state is inline-only, so suppress Form.io's
            // aggregated `.alert-danger` summary banner. Per-field errors
            // remain, and handleBlockedSubmit replaces the summary's jump links
            // by moving focus to the first invalid field.
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
            otherEvents={{ "formio.componentError": handleBlockedSubmit }}
          />
        </div>
      )}

      {/* Rendered unconditionally and filled later: a live region that appears
          in the same commit as its text is announced unreliably. */}
      <div role="status" className={styles.visuallyHidden}>
        {blockedMessage}
      </div>

      {/* Inline "not eligible" warning. Shows as soon as either question is
          answered "No"; the rest of the form stays hidden in that state, so this
          sits directly under the visible questions.
          aria-live on a wrapper that is always mounted, rather than role="alert"
          on the InlineAlert: the alert sets aria-labelledby, so some screen
          readers would announce only the title and drop the body. */}
      <div className={styles.statusWarning} aria-live="polite">
        {showStatusWarning && (
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
                rel="noopener noreferrer"
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
        )}
      </div>

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
                  <SvgExclamationCircleIcon />
                  This is only an estimate. The actual amount may be different.
                </p>
              </div>

              <h2 className={styles.subHeading}>How your estimate was calculated</h2>
              <p className={styles.prose}>
                The estimated amount is based on your household information and the{" "}
                <RatesLink /> for support and shelter allowance.
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
                  <SvgExclamationCircleIcon />
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
