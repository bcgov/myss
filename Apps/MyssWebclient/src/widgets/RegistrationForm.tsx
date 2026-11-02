import { Button, InlineAlert } from "@bcgov/design-system-react-components";
import type { Webform } from "@formio/js";
import { Form } from "@formio/react";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

import { SubmissionRejectedError, type FormValidationError } from "@/api/forms";
import { ME_QUERY_KEY } from "@/auth/useMe";
import {
  AccountRequestError,
  EMAIL_TOO_SOON_KEYWORD,
  registrationEmailQueryKey,
  useRequestRegistrationEmail,
  type RegistrationEmail,
} from "@/hooks/useAccount";
import { useFeature } from "@/hooks/useFeatures";
import {
  SubmissionErrors,
  useClientValidation,
} from "@myss/web-common/components";
import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";
import { checkNewPin, REGISTRATION_PIN_FIELDS as PIN_FIELDS } from "@/lib/pin";
import CreatePinSection from "./CreatePinSection";
import RegistrationReview from "./registration/RegistrationReview";
import RegistrationSteps, {
  type RegistrationStep,
} from "./registration/RegistrationSteps";
import VerifyEmailStep from "./registration/VerifyEmailStep";
import {
  CONSENT_KEYS,
  prepareRegistrationSpec,
  reviewRows,
  splitRegistrationSpec,
  type IdentityDetails,
} from "./registrationSpec";
import styles from "./RegistrationForm.module.css";

const FORM_SPEC_ID = "registration";

/** The keyword the API refuses a registration with until Step 2 is done. */
const EMAIL_NOT_VERIFIED_KEYWORD = "REGISTRATION.EMAIL.NOT_VERIFIED";

type Step = "info" | "verify" | "review";

function isPinField(field: string): boolean {
  return field === PIN_FIELDS.pin || field === PIN_FIELDS.confirmation;
}

/** A field answered in Step 1: neither the PIN nor the consent, which are Step 3's. */
function isInfoField(field: string): boolean {
  return !isPinField(field) && !CONSENT_KEYS.has(field);
}

function byField(
  errors: readonly FormValidationError[],
): Record<string, string> {
  return Object.fromEntries(
    errors.map((error) => [error.field, error.message]),
  );
}

function checkRegistrationPin(pin: string, confirmation: string) {
  return checkNewPin(pin, confirmation, PIN_FIELDS);
}

function sameAddress(a?: string | null, b?: string | null): boolean {
  return (a ?? "").trim().toLowerCase() === (b ?? "").trim().toLowerCase();
}

interface RegistrationFormProps {
  /** What the sign-in identity knows about the citizen, used to prefill. */
  identity: IdentityDetails;
  /**
   * Whether the citizen creates a PIN as part of registering: Basic BCeID
   * users do (MYSS-258), BC Services Card users do not.
   */
  requirePin?: boolean;
  /** How the citizen signed in, for the first line of the steps. */
  signedInWith?: string;
  /** The signed-in subject, which scopes the cached Step 2 status. */
  subject?: string;
  /** Called once the API has accepted the registration. */
  onRegistered: () => void;
  onCancel: () => void;
}

/**
 * Creates the citizen's account in three steps (MYSS-295): the registration
 * form authored in Strapi, prefilled from the sign-in identity; proving the
 * email address by an emailed link (behind the EmailVerification flag); and
 * a review with the PIN and the consent before the one submission.
 *
 * Step 1's Form.io form stays mounted, hidden, through the later steps: it
 * holds the answers, and when the API refuses one of them the citizen is taken
 * back to it with the message on the field.
 */
export default function RegistrationForm({
  identity,
  requirePin = false,
  signedInWith = "BCeID",
  subject = "",
  onRegistered,
  onCancel,
}: RegistrationFormProps) {
  const queryClient = useQueryClient();
  const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
  const submit = useSubmitForm(FORM_SPEC_ID);
  const requestEmail = useRequestRegistrationEmail(subject);
  // undefined while the flags load: Continue waits for them, so Step 1 never
  // asks for a link the API has switched off (and answers 404 for). A flag
  // that cannot be read counts as on, as useFeature says.
  const emailVerificationFlag = useFeature("EmailVerification");
  const verifyEmail = emailVerificationFlag !== false;
  const flagsReady = emailVerificationFlag !== undefined;
  const [step, setStep] = useState<Step>("info");
  // Read by Step 1's submit handler: Form.io delivers Step 3's consent submit
  // to it as well (measured 2026-10-27), and that must not replace the answers.
  const stepRef = useRef<Step>("info");
  stepRef.current = step;
  // Step 1's answers, kept once it passes, for Step 2's address and Step 3.
  // The ref is what the submit handlers read: Form.io can hold on to an
  // earlier render's onSubmit, so state read there could be stale.
  const [answers, setAnswers] = useState<Record<string, unknown>>({});
  const answersRef = useRef<Record<string, unknown>>({});
  const verifyEmailRef = useRef(verifyEmail);
  verifyEmailRef.current = verifyEmail;
  // A refusal of Step 1's Continue that is not a field error (429, 503).
  const [continueProblem, setContinueProblem] = useState<string | null>(null);
  // Step 2's 422 on the address, shown on Step 1's email field.
  const [emailRefusal, setEmailRefusal] =
    useState<SubmissionRejectedError | null>(null);

  const formRef = useRef<Webform | null>(null);
  const consentFormRef = useRef<Webform | null>(null);
  // Set the moment a submission starts. submit.isPending only changes on the
  // next render, so two quick presses could both read it as false and send the
  // registration twice; a ref is current at once.
  const isSubmittingRef = useRef(false);
  // The refusal whose errors are on the fields now, so a re-render does not
  // put them back after the citizen has started fixing them.
  const displayedErrorRef = useRef<Error | null>(null);
  const stepHeadingRef = useRef<HTMLHeadingElement>(null);

  // The refusal Step 1's fields show: the address's from Step 2, or the
  // registration's own.
  const fieldRefusal: Error | null = emailRefusal ?? submit.error;
  const validationErrors = useMemo(
    () =>
      fieldRefusal instanceof SubmissionRejectedError
        ? fieldRefusal.errors
        : [],
    [fieldRefusal],
  );
  // A submit Form.io blocks is listed in the same summary as one the API
  // refuses; Form.io's own alert is switched off so nothing shows twice.
  const clientValidation = useClientValidation();
  const formOptions = useMemo(() => ({ noAlerts: true }), []);

  // The PIN lives outside the Form.io form (registration answers are stored
  // as submitted) and is sent beside the answers. The ref is what the submit
  // handlers read: Form.io can hold on to an earlier render's onSubmit.
  const [pin, setPin] = useState("");
  const [pinConfirmation, setPinConfirmation] = useState("");
  const pinRef = useRef({ pin: "", confirmation: "" });
  // What the PIN fields show now: cleared field by field as the citizen edits.
  const [pinErrors, setPinErrors] = useState<Record<string, string>>({});
  // What the summary lists for the last attempt. A snapshot, so editing a
  // field does not rebuild the summary and pull focus back to it.
  const [pinAttemptErrors, setPinAttemptErrors] = useState<
    FormValidationError[]
  >([]);
  const { reset: resetSubmit } = submit;

  // Keyed on the individual values, not the identity object: the session
  // builds a new user object every render, and a new `src` makes Form.io
  // rebuild the form and drop whatever the citizen has typed.
  const { givenName, familyName, email, phoneNumber, birthdate, gender } =
    identity;
  const parts = useMemo(
    () =>
      spec &&
      splitRegistrationSpec(
        prepareRegistrationSpec(spec.spec, {
          givenName,
          familyName,
          email,
          phoneNumber,
          birthdate,
          gender,
        }),
      ),
    [spec, givenName, familyName, email, phoneNumber, birthdate, gender],
  );

  const steps: RegistrationStep[] = useMemo(
    () => [
      { label: `Sign in with ${signedInWith}` },
      { prefix: "Step 1:", label: "Provide your information" },
      ...(verifyEmail
        ? [{ prefix: "Step 2:", label: "Verify your email" }]
        : []),
      {
        prefix: verifyEmail ? "Step 3:" : "Step 2:",
        label: "Review and submit",
      },
    ],
    [signedInWith, verifyEmail],
  );
  const stepIndex = { info: 1, verify: 2, review: verifyEmail ? 3 : 2 }[step];

  // Each step's heading takes focus as the step opens, so a screen-reader
  // user hears where they are. Step 2 focuses its own.
  useEffect(() => {
    if (step !== "verify") stepHeadingRef.current?.focus();
  }, [step]);

  const goTo = useCallback((next: Step) => {
    setContinueProblem(null);
    setStep(next);
    window.scrollTo?.({ top: 0 });
  }, []);

  const handleFormReady = useCallback((instance: Webform) => {
    formRef.current = instance;
  }, []);

  // Complete registration waits for the consent form: pressed before it is
  // ready, the consent could not be asked for, and must not be skipped.
  const [consentReady, setConsentReady] = useState(false);
  const handleConsentReady = useCallback((instance: Webform) => {
    consentFormRef.current = instance;
    setConsentReady(true);
  }, []);

  // The consent form goes when Step 3 does; so does the hold on it.
  useEffect(() => {
    if (step !== "review") {
      consentFormRef.current = null;
      setConsentReady(false);
    }
  }, [step]);

  /** Step 1's Continue: Form.io checks the answers, then onInfoSubmit runs. */
  const handleContinue = useCallback(() => {
    setContinueProblem(null);
    setEmailRefusal(null);
    resetSubmit();
    // Rejects when client-side validation fails; the messages are already on
    // the fields and in the summary (onSubmitError), so nothing more to do.
    void formRef.current?.submit().catch(() => undefined);
  }, [resetSubmit]);

  const onInfoSubmit = useCallback(
    (data: Record<string, unknown>) => {
      if (stepRef.current !== "info") return;
      clientValidation.clear();
      answersRef.current = data;
      setAnswers(data);
      if (!verifyEmailRef.current) {
        goTo("review");
        return;
      }

      const address = typeof data.email === "string" ? data.email.trim() : "";
      const known = queryClient.getQueryData<RegistrationEmail>(
        registrationEmailQueryKey(subject),
      );
      requestEmail.mutate(address, {
        // Already proven in this registration (AC6): straight to the review.
        onSuccess: (status) =>
          goTo(
            status.verified && sameAddress(status.email, address)
              ? "review"
              : "verify",
          ),
        onError: (requestError) => {
          if (!(requestError instanceof AccountRequestError)) {
            setContinueProblem(
              `We could not send the verification email. ${requestError.message}`,
            );
            return;
          }
          if (requestError.status === 422) {
            setEmailRefusal(
              new SubmissionRejectedError(422, [...requestError.errors]),
            );
            return;
          }
          // Inside the cooldown for the same address, the link is already on
          // its way: Step 2 is where to wait for it.
          if (
            requestError.keyword === EMAIL_TOO_SOON_KEYWORD &&
            sameAddress(known?.email, address)
          ) {
            goTo("verify");
            return;
          }
          setContinueProblem(
            requestError.keyword === EMAIL_TOO_SOON_KEYWORD
              ? requestError.message
              : `We could not send the verification email. ${requestError.message}`,
          );
        },
      });
    },
    [clientValidation, goTo, queryClient, requestEmail, subject],
  );

  /** Step 3's Complete registration: the PIN and the consent, then the API. */
  function handleComplete() {
    if (isSubmittingRef.current) return;
    // A new attempt: the last refusal's summary gives way to this one's.
    resetSubmit();
    if (requirePin) {
      const found = checkRegistrationPin(
        pinRef.current.pin,
        pinRef.current.confirmation,
      );
      setPinAttemptErrors(found);
      setPinErrors(byField(found));
    }
    const consentForm = consentFormRef.current;
    if (parts?.consent && !consentForm) {
      // Not ready yet (the button waits for it too): never send without it.
      return;
    }
    if (consentForm) {
      // Rejects while the consent is unchecked; the message is on it already.
      void consentForm.submit().catch(() => undefined);
    } else {
      sendRegistration({});
    }
  }

  function sendRegistration(consent: Record<string, unknown>) {
    if (isSubmittingRef.current || !spec) return;
    const { pin: enteredPin, confirmation } = pinRef.current;
    // The PIN's errors are already on the fields and in the summary.
    if (
      requirePin &&
      checkRegistrationPin(enteredPin, confirmation).length > 0
    ) {
      consentFormRef.current?.emit("cancelSubmit");
      return;
    }
    isSubmittingRef.current = true;
    setPinAttemptErrors([]);
    submit.mutate(
      {
        formSpecVersion: spec.version,
        answers: { ...answersRef.current, ...consent },
        ...(requirePin
          ? { pin: enteredPin, pinConfirmation: confirmation }
          : {}),
      },
      {
        onSuccess: () => {
          isSubmittingRef.current = false;
          // Tell the page first, so it shows the confirmation rather than
          // redirecting when the refreshed /me reports a profile.
          onRegistered();
          void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
        },
        onError: (submitError) => {
          // Released so the citizen can fix the answers and try again.
          isSubmittingRef.current = false;
          consentFormRef.current?.emit("cancelSubmit");
          if (!(submitError instanceof SubmissionRejectedError)) return;
          setPinErrors(
            byField(submitError.errors.filter((e) => isPinField(e.field))),
          );
          const notVerified = submitError.errors.some(
            (e) => e.keyword === EMAIL_NOT_VERIFIED_KEYWORD,
          );
          if (notVerified && submitError.errors.length === 1) {
            // The address lost its proof (another tab, a new link): Step 2.
            goTo("verify");
          } else if (submitError.errors.some((e) => isInfoField(e.field))) {
            // An answer from Step 1 was refused: back there, message on it.
            goTo("info");
          }
        },
      },
    );
  }

  const handlePinChange = useCallback((value: string) => {
    pinRef.current.pin = value;
    setPin(value);
    // The confirmation's mismatch depends on this value too.
    setPinErrors({});
  }, []);

  const handlePinConfirmationChange = useCallback((value: string) => {
    pinRef.current.confirmation = value;
    setPinConfirmation(value);
    setPinErrors((current) =>
      Object.fromEntries(
        Object.entries(current).filter(
          ([field]) => field !== PIN_FIELDS.confirmation,
        ),
      ),
    );
  }, []);

  // Put the API's field errors (date of birth, phone, gender...) on Step 1's
  // fields themselves, not just in the summary, and focus the first one. Same
  // as the other Form.io widgets. focusOnComponent goes by component key, so
  // it also reaches the custom radio group, whose inputs have generated names
  // the summary's links cannot find.
  useEffect(() => {
    const form = formRef.current;
    if (
      step !== "info" ||
      !form ||
      validationErrors.length === 0 ||
      displayedErrorRef.current === fieldRefusal
    ) {
      return;
    }
    displayedErrorRef.current = fieldRefusal;

    // The PIN's and the consent's errors are Step 3's; Form.io's Step 1 form
    // has no component by those names.
    const formioErrors = validationErrors
      .filter((validationError) => isInfoField(validationError.field))
      .map((validationError) => ({
        level: "error",
        message: validationError.message,
        path: validationError.field,
      }));

    form.clearServerErrors();
    form.setServerErrors({ details: formioErrors });
    form.showErrors(formioErrors, false);
    form.emit("cancelSubmit");

    const first = formioErrors[0]?.path;
    if (first) form.focusOnComponent(first);
  }, [step, fieldRefusal, validationErrors]);

  // On Step 3, the PIN's API error goes on the PIN field and takes focus.
  useEffect(() => {
    if (step !== "review" || !(submit.error instanceof SubmissionRejectedError))
      return;
    const first = submit.error.errors[0]?.field;
    if (first && isPinField(first)) {
      document
        .querySelector<HTMLInputElement>(`[name="data[${CSS.escape(first)}]"]`)
        ?.focus();
    }
  }, [step, submit.error]);

  // Take a server error off its field as soon as the citizen changes that
  // field, rather than leaving the stale message until the next submit (when
  // Form.io clears them all). Form.io's change event names the component, so
  // this works for every field type, the radio group included.
  const handleChange = useCallback(
    (value: { changed?: { component?: { key?: string } } }) => {
      const field = value.changed?.component?.key;
      const form = formRef.current;
      const displayed = displayedErrorRef.current;
      if (
        !field ||
        !form ||
        !(displayed instanceof SubmissionRejectedError) ||
        !displayed.errors.some((error) => error.field === field)
      ) {
        return;
      }

      form.serverErrors = (form.serverErrors ?? []).filter(
        (serverError: { path?: string }) => serverError.path !== field,
      );
      const component = form.getComponent(field);
      if (component) {
        component.serverErrors = [];
        component.setCustomValidity([], false, true);
      }

      if (form.serverErrors.length === 0) {
        displayedErrorRef.current = null;
      }
    },
    [],
  );

  // One summary per step for every reason it could not go on: Form.io's own
  // checks (and on Step 3 the PIN's), or else what the API refused.
  const summaryError = useMemo(() => {
    if (step === "info") {
      if (clientValidation.error) return clientValidation.error;
      return fieldRefusal instanceof SubmissionRejectedError &&
        fieldRefusal.errors.some((e) => isInfoField(e.field))
        ? fieldRefusal
        : null;
    }
    if (step !== "review") return null;
    if (clientValidation.error || pinAttemptErrors.length > 0) {
      const consentErrors = clientValidation.error?.errors ?? [];
      return new SubmissionRejectedError(0, [
        ...consentErrors,
        ...pinAttemptErrors,
      ]);
    }
    // A refusal of Step 1's answers is shown there; anything else here.
    if (
      submit.error instanceof SubmissionRejectedError &&
      submit.error.errors.every((e) => isInfoField(e.field))
    ) {
      return null;
    }
    return submit.error;
  }, [
    step,
    clientValidation.error,
    fieldRefusal,
    submit.error,
    pinAttemptErrors,
  ]);

  const rows = useMemo(
    () => (parts ? reviewRows(parts.info, answers) : []),
    [parts, answers],
  );

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  return (
    <section className={styles.registration}>
      <RegistrationSteps steps={steps} current={stepIndex} />

      {/* Step 1. Mounted throughout: it holds the answers, and a refused
          answer brings the citizen back to it with the message on the field. */}
      <div hidden={step !== "info"} className={styles.step}>
        <h2 ref={step === "info" ? stepHeadingRef : undefined} tabIndex={-1}>
          Step 1: Provide your information
        </h2>
        {step === "info" && summaryError && (
          <SubmissionErrors error={summaryError} />
        )}
        <Form
          src={parts?.info ?? spec.spec}
          options={formOptions}
          onFormReady={handleFormReady}
          onChange={handleChange}
          onSubmitError={clientValidation.onSubmitError}
          onSubmit={(submission: { data: Record<string, unknown> }) =>
            onInfoSubmit(submission.data)
          }
        />
        {continueProblem && (
          <InlineAlert
            variant="danger"
            role="alert"
            description={continueProblem}
          />
        )}
        <div className={styles.actions}>
          <Button variant="secondary" onPress={onCancel}>
            Cancel
          </Button>
          <Button
            variant="primary"
            onPress={handleContinue}
            isDisabled={requestEmail.isPending || !flagsReady}
          >
            Continue
          </Button>
        </div>
      </div>

      {step === "verify" && (
        <VerifyEmailStep
          email={typeof answers.email === "string" ? answers.email.trim() : ""}
          subject={subject}
          onBack={() => goTo("info")}
          onVerified={() => goTo("review")}
        />
      )}

      {step === "review" && (
        <div className={styles.step}>
          <h2 ref={stepHeadingRef} tabIndex={-1}>
            {verifyEmail ? "Step 3" : "Step 2"}: Review and submit
          </h2>
          {summaryError && <SubmissionErrors error={summaryError} />}
          {requirePin && (
            <CreatePinSection
              pin={pin}
              confirmation={pinConfirmation}
              onPinChange={handlePinChange}
              onConfirmationChange={handlePinConfirmationChange}
              errors={pinErrors}
            />
          )}
          <h3>Review your information</h3>
          <RegistrationReview rows={rows} />
          <InlineAlert
            variant="info"
            title="Need to make a change?"
            description="Go back to update the information you provided."
          />
          {parts?.consent && (
            <Form
              src={parts.consent}
              options={formOptions}
              onFormReady={handleConsentReady}
              onSubmitError={clientValidation.onSubmitError}
              onSubmit={(submission: { data: Record<string, unknown> }) => {
                clientValidation.clear();
                sendRegistration(submission.data);
              }}
            />
          )}
          <div className={styles.actions}>
            <Button
              variant="secondary"
              onPress={() => goTo("info")}
              isDisabled={submit.isPending}
            >
              Back
            </Button>
            <Button
              variant="primary"
              onPress={handleComplete}
              isDisabled={submit.isPending || (Boolean(parts?.consent) && !consentReady)}
            >
              Complete registration
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}
