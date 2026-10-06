import { Form } from "@formio/react";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo } from "react";
import { useNavigate } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

import { ME_QUERY_KEY } from "@/auth/useMe";
import SubmissionErrors from "@/components/SubmissionErrors";
import { useClientValidation } from "@/hooks/useClientValidation";
import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";
import { paths } from "@/routes/paths";
import styles from "./RegistrationForm.module.css";

const FORM_SPEC_ID = "registration";

/**
 * Renders and submits the authenticated registration form authored in Strapi.
 */
export default function RegistrationForm() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
  const submit = useSubmitForm(FORM_SPEC_ID);
  // A submit Form.io blocks is listed in the same summary as one the API
  // refuses; Form.io's own alert is switched off so nothing shows twice.
  const clientValidation = useClientValidation();
  const formOptions = useMemo(() => ({ noAlerts: true }), []);

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

  const summaryError = clientValidation.error ?? submit.error;

  return (
    <section className={styles.form}>
      <h2>{spec.title ?? "Registration information"}</h2>
      {summaryError && <SubmissionErrors error={summaryError} />}
      <Form
        src={spec.spec}
        options={formOptions}
        onSubmitError={clientValidation.onSubmitError}
        onSubmit={(submission: { data: Record<string, unknown> }) => {
          clientValidation.clear();
          submit.mutate({
            formSpecVersion: spec.version,
            answers: submission.data,
          });
        }}
      />
    </section>
  );
}
