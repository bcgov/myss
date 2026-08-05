import { Form } from "@formio/react";
import { Link } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./PocForm.css";

import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "poc-test-form";

/**
 * Renders the Form.io spec fetched through the MyssApi proxy and posts the
 * answers back with the spec version they were rendered under.
 */
export default function PocForm() {
  const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
  const submit = useSubmitForm(FORM_SPEC_ID);

  if (isPending) return <p>Loading form…</p>;
  if (error) return <p>Could not load the form: {error.message}</p>;

  if (submit.data) {
    return (
      <section aria-live="polite">
        <h2>Submission received</h2>
        <dl>
          <dt>Submission ID</dt>
          <dd>
            <code>{submit.data.id}</code>
          </dd>
          <dt>Stored against spec version</dt>
          <dd>
            {submit.data.formSpecId} v{submit.data.formSpecVersion}
          </dd>
        </dl>
        <Link to={`/techdemos/forms/submissions/${submit.data.id}`}>
          View this submission
        </Link>
      </section>
    );
  }

  return (
    <section>
      <h3>
        {spec.title ?? spec.formSpecId} <small>(spec v{spec.version})</small>
      </h3>
      {submit.error && <p>Submission failed: {submit.error.message}</p>}
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
