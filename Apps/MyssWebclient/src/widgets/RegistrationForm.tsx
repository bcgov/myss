import { Button } from "@bcgov/design-system-react-components";
import type { Webform } from "@formio/js";
import { Form } from "@formio/react";
import { useQueryClient } from "@tanstack/react-query";
import { useCallback, useMemo, useRef } from "react";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

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
    // Rejects when client-side validation fails; Form.io has already shown
    // the messages on the fields, so there is nothing more to do here.
    void formRef.current?.submit().catch(() => undefined);
  }, []);
  
  // The dashboard decides whether to send a citizen here from the cached
  // /me response, which stays fresh for minutes. Refresh it and wait for the
  // new answer before navigating, or the freshly registered citizen is
  // bounced straight back to this page.
  useEffect(() => {
    if (!submit.data) return;
    let cancelled = false;
    void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }).then(() => {
      if (!cancelled) void navigate(paths.dashboard, { replace: true });
    });
    return () => {
      cancelled = true;
    };
  }, [navigate, queryClient, submit.data]);

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  if (submit.data)
    return <output>Registration complete. Loading your dashboard…</output>;

  return (
    <section className={styles.form}>
      {submit.error && <SubmissionErrors error={submit.error} />}
      <Form
        src={prepared ?? spec.spec}
        onFormReady={handleFormReady}
        onSubmit={(submission: { data: Record<string, unknown> }) =>
          submit.mutate(
            { formSpecVersion: spec.version, answers: submission.data },
            {
              onSuccess: () => {
                // Tell the page first, so it shows the confirmation rather
                // than redirecting when the refreshed /me reports a profile.
                onRegistered();
                void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY });
              },
              onError: () => formRef.current?.emit("cancelSubmit"),
            },
          )
        }
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
