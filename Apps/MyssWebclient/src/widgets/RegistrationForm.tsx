import { Button } from "@bcgov/design-system-react-components";
import type { Webform } from "@formio/js";
import { Form } from "@formio/react";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useMemo, useRef } from "react";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

import { SubmissionRejectedError } from "@/api/forms";
import { ME_QUERY_KEY } from "@/auth/useMe";
import SubmissionErrors from "@/components/SubmissionErrors";
import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";
import {
  prepareRegistrationSpec,
  type IdentityDetails,
} from "./registrationSpec";
import styles from "./RegistrationForm.module.css";

const FORM_SPEC_ID = "registration";

interface RegistrationFormProps {
  /** What the sign-in identity knows about the citizen, used to prefill. */
  identity: IdentityDetails;
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
    // Rejects when client-side validation fails; Form.io has already shown
    // the messages on the fields, so there is nothing more to do here.
    void formRef.current?.submit().catch(() => undefined);
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

    const formioErrors = validationErrors.map((validationError) => ({
      level: "error",
      message: validationError.message,
      path: validationError.field,
    }));

    form.clearServerErrors();
    form.setServerErrors({ details: formioErrors });
    form.showErrors(formioErrors, false);
    form.emit("cancelSubmit");

    const first = validationErrors[0]?.field;
    if (first) form.focusOnComponent(first);
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

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  return (
    <section className={styles.form}>
      {submit.error && <SubmissionErrors error={submit.error} />}
      <Form
        src={prepared ?? spec.spec}
        onFormReady={handleFormReady}
        onChange={handleChange}
        onSubmit={(submission: { data: Record<string, unknown> }) => {
          if (isSubmittingRef.current) return;
          isSubmittingRef.current = true;
          submit.mutate(
            { formSpecVersion: spec.version, answers: submission.data },
            {
              onSuccess: () => {
                isSubmittingRef.current = false;
                // Tell the page first, so it shows the confirmation rather
                // than redirecting when the refreshed /me reports a profile.
                onRegistered();
                void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
              },
              onError: () => {
                // Released so the citizen can fix the answers and try again.
                isSubmittingRef.current = false;
                formRef.current?.emit("cancelSubmit");
              },
            },
          );
        }}
      />
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
