import { Form } from "@formio/react";
import { useEffect } from "react";
import { useNavigate } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

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
  const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
  const submit = useSubmitForm(FORM_SPEC_ID);

  useEffect(() => {
    if (submit.data) {
      navigate(paths.dashboard, { replace: true });
    }
  }, [navigate, submit.data]);

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  if (submit.data) return <p role="status">Registration complete. Loading your dashboard…</p>;

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
