import { Form } from "@formio/react";
import { Link } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./PocForm.css";

import SubmissionErrors from "@/components/SubmissionErrors";
import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "registration";

/**
 * Renders and submits the authenticated registration form authored in Strapi.
 */
export default function RegistrationForm() {
  const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
  const submit = useSubmitForm(FORM_SPEC_ID);

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  if (submit.data) {
    return (
      <section aria-live="polite">
        <h2>Registration information received</h2>
        <Link to="/dashboard">Continue to MySS</Link>
      </section>
    );
  }

  return (
    <section>
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
