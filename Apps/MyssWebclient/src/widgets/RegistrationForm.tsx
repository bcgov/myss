import { Form } from "@formio/react";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useNavigate } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

import { ME_QUERY_KEY } from "@/auth/useMe";
import SubmissionErrors from "@/components/SubmissionErrors";
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

  // The dashboard decides whether to send a citizen here from the cached
  // /me response, which stays fresh for minutes. Refresh it and wait for the
  // new answer before navigating, or the freshly registered citizen is
  // bounced straight back to this page.
  useEffect(() => {
    if (!submit.data) return;
    let cancelled = false;
    void queryClient.invalidateQueries({ queryKey: ME_QUERY_KEY }).then(() => {
      if (!cancelled) navigate(paths.dashboard, { replace: true });
    });
    return () => {
      cancelled = true;
    };
  }, [navigate, queryClient, submit.data]);

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  if (submit.data)
    return <p role="status">Registration complete. Loading your dashboard…</p>;

  return (
    <section className={styles.form}>
      <h2>{spec.title ?? "Registration information"}</h2>
      {submit.error && <SubmissionErrors error={submit.error} />}
      <Form
        src={spec.spec}
        onSubmit={(submission: { data: Record<string, unknown> }) =>
          submit.mutate({
            formSpecVersion: spec.version,
            answers: submission.data,
          })
        }
      />
    </section>
  );
}
