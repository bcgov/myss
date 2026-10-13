import { Button } from "@bcgov/design-system-react-components";
import type { Webform } from "@formio/js";
import { Form } from "@formio/react";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

import {
  SubmissionRejectedError,
  type FormValidationError,
} from "@/api/forms";
import { ME_QUERY_KEY } from "@/auth/useMe";
import SubmissionErrors from "@/components/SubmissionErrors";
import { useClientValidation } from "@/hooks/useClientValidation";
import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";
import {
  checkNewPin,
  REGISTRATION_PIN_FIELDS as PIN_FIELDS,
} from "@/lib/pin";
import CreatePinSection from "./CreatePinSection";
import {
  prepareRegistrationSpec,
  type IdentityDetails,
} from "./registrationSpec";
import styles from "./RegistrationForm.module.css";

const FORM_SPEC_ID = "registration";

function isPinField(field: string): boolean {
  return field === PIN_FIELDS.pin || field === PIN_FIELDS.confirmation;
}

function byField(errors: readonly FormValidationError[]): Record<string, string> {
  return Object.fromEntries(errors.map((error) => [error.field, error.message]));
}

function checkRegistrationPin(pin: string, confirmation: string) {
  return checkNewPin(pin, confirmation, PIN_FIELDS);
}

interface RegistrationFormProps {
  /** What the sign-in identity knows about the citizen, used to prefill. */
  identity: IdentityDetails;
  /**
   * Whether the citizen creates a PIN as part of registering: Basic BCeID
   * users do (MYSS-258), BC Services Card users do not.
   */
  requirePin?: boolean;
  /** Called once the API has accepted the registration. */
  onRegistered: () => void;
  onCancel: () => void;
}

/**
 * Renders and submits the authenticated registration form authored in Strapi,
 * prefilled from the sign-in identity, with BC Gov Design System buttons in
 * place of the spec's own submit button.
 */
export default function RegistrationForm({
  identity,
  requirePin = false,
  onRegistered,
  onCancel,
}: RegistrationFormProps) {
  const queryClient = useQueryClient();
  const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
  const submit = useSubmitForm(FORM_SPEC_ID);
  const formRef = useRef<Webform | null>(null);
  // Set the moment a submission starts. submit.isPending only changes on the
  // next render, so two quick presses could both read it as false and send the
  // registration twice; a ref is current at once.
  const isSubmittingRef = useRef(false);
  // The refusal whose errors are on the fields now, so a re-render does not
  // put them back after the citizen has started fixing them.
  const displayedErrorRef = useRef<Error | null>(null);
  const validationErrors = useMemo(
    () =>
      submit.error instanceof SubmissionRejectedError
        ? submit.error.errors
        : [],
    [submit.error],
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
  const prepared = useMemo(
    () =>
      spec &&
      prepareRegistrationSpec(spec.spec, {
        givenName,
        familyName,
        email,
        phoneNumber,
        birthdate,
        gender,
      }),
    [spec, givenName, familyName, email, phoneNumber, birthdate, gender],
  );

  const handleFormReady = useCallback((instance: Webform) => {
    formRef.current = instance;
  }, []);

  const handleComplete = useCallback(() => {
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
    // Rejects when client-side validation fails; the messages are already on
    // the fields and in the summary (onSubmitError), so nothing more to do.
    void formRef.current?.submit().catch(() => undefined);
  }, [requirePin, resetSubmit]);

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

  // Put the API's field errors (date of birth, phone, gender...) on the fields
  // themselves, not just in the summary, and focus the first one. Same as the
  // other Form.io widgets (ApplicationForm, FormSpecWidget). focusOnComponent
  // goes by component key, so it also reaches the custom radio group, whose
  // inputs have generated names the summary's links cannot find.
  useEffect(() => {
    const form = formRef.current;
    if (
      !form ||
      validationErrors.length === 0 ||
      displayedErrorRef.current === submit.error
    ) {
      return;
    }
    displayedErrorRef.current = submit.error;

    // The PIN's errors go on the PIN fields (onError), not into Form.io,
    // which has no component by those names.
    const formioErrors = validationErrors
      .filter((validationError) => !isPinField(validationError.field))
      .map((validationError) => ({
        level: "error",
        message: validationError.message,
        path: validationError.field,
      }));

    form.clearServerErrors();
    form.setServerErrors({ details: formioErrors });
    form.showErrors(formioErrors, false);
    form.emit("cancelSubmit");

    const first = validationErrors[0]?.field;
    if (first && isPinField(first)) {
      document
        .querySelector<HTMLInputElement>(`[name="data[${CSS.escape(first)}]"]`)
        ?.focus();
    } else if (first) {
      form.focusOnComponent(first);
    }
  }, [submit.error, validationErrors]);

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

  // One summary for every reason the attempt failed: Form.io's own checks
  // and the PIN's together, or else what the API refused.
  const summaryError = useMemo(() => {
    if (clientValidation.error) {
      return pinAttemptErrors.length > 0
        ? new SubmissionRejectedError(0, [
            ...clientValidation.error.errors,
            ...pinAttemptErrors,
          ])
        : clientValidation.error;
    }
    if (submit.error) return submit.error;
    return pinAttemptErrors.length > 0
      ? new SubmissionRejectedError(0, pinAttemptErrors)
      : null;
  }, [clientValidation.error, submit.error, pinAttemptErrors]);

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  return (
    <section className={styles.form}>
      {summaryError && <SubmissionErrors error={summaryError} />}
      <Form
        src={prepared ?? spec.spec}
        options={formOptions}
        onFormReady={handleFormReady}
        onChange={handleChange}
        onSubmitError={clientValidation.onSubmitError}
        onSubmit={(submission: { data: Record<string, unknown> }) => {
          if (isSubmittingRef.current) return;
          clientValidation.clear();
          const { pin: enteredPin, confirmation } = pinRef.current;
          // The form is valid but the PIN is not: its errors are already on
          // the fields and in the summary (handleComplete).
          if (
            requirePin &&
            checkRegistrationPin(enteredPin, confirmation).length > 0
          ) {
            formRef.current?.emit("cancelSubmit");
            return;
          }
          isSubmittingRef.current = true;
          setPinAttemptErrors([]);
          submit.mutate(
            {
              formSpecVersion: spec.version,
              answers: submission.data,
              ...(requirePin
                ? { pin: enteredPin, pinConfirmation: confirmation }
                : {}),
            },
            {
              onSuccess: () => {
                isSubmittingRef.current = false;
                // Tell the page first, so it shows the confirmation rather
                // than redirecting when the refreshed /me reports a profile.
                onRegistered();
                void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
              },
              onError: (submitError) => {
                // Released so the citizen can fix the answers and try again.
                isSubmittingRef.current = false;
                formRef.current?.emit("cancelSubmit");
                if (submitError instanceof SubmissionRejectedError) {
                  setPinErrors(
                    byField(
                      submitError.errors.filter((e) => isPinField(e.field)),
                    ),
                  );
                }
              },
            },
          );
        }}
      />
      {requirePin && (
        <CreatePinSection
          pin={pin}
          confirmation={pinConfirmation}
          onPinChange={handlePinChange}
          onConfirmationChange={handlePinConfirmationChange}
          errors={pinErrors}
        />
      )}
      <div className={styles.actions}>
        <Button variant="secondary" onPress={onCancel}>
          Cancel
        </Button>
        <Button
          variant="primary"
          onPress={handleComplete}
          isDisabled={submit.isPending}
        >
          Complete registration
        </Button>
      </div>
    </section>
  );
}
