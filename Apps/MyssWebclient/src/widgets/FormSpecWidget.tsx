import { Form } from "@formio/react";
import type { ReactNode } from "react";
import { Link } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";

interface FormSpecWidgetProps {
  formSpecId: string;
  renderSubmissionError?: (error: Error) => ReactNode;
  showSpecHeading?: boolean;
}

export default function FormSpecWidget({
  formSpecId,
  renderSubmissionError,
  showSpecHeading = false,
}: FormSpecWidgetProps) {
  const { data: spec, error, isPending } = useFormSpec(formSpecId);
  const submit = useSubmitForm(formSpecId);

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
      {showSpecHeading && (
        <h3>
          {spec.title ?? spec.formSpecId} <small>(spec v{spec.version})</small>
        </h3>
      )}
      {submit.error &&
        (renderSubmissionError ? (
          renderSubmissionError(submit.error)
        ) : (
          <p>Submission failed: {submit.error.message}</p>
        ))}
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
