import { Form } from "@formio/react";
import { Link, useParams } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "@/components/PocForm.css";

import { useSubmission } from "@/hooks/usePocForm";

/**
 * Renders a stored submission read-only, using the spec version it was
 * submitted under rather than the current one.
 */
export default function SubmissionView() {
  const { id } = useParams<{ id: string }>();
  const { data: submission, error, isPending } = useSubmission(id!);

  if (isPending) return <p>Loading submission…</p>;
  if (error) return <p>Could not load the submission: {error.message}</p>;

  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to="/techdemos/forms">← Forms tech demo</Link>
      </nav>
      <h1>Submission</h1>
      <dl>
        <dt>Submission ID</dt>
        <dd>
          <code>{submission.id}</code>
        </dd>
        <dt>Submitted</dt>
        <dd>{new Date(submission.submittedAt).toLocaleString()}</dd>
        <dt>Rendered from archived spec</dt>
        <dd>
          {submission.formSpecId} v{submission.formSpecVersion}
        </dd>
      </dl>
      {submission.spec ? (
        <Form
          src={submission.spec.spec}
          submission={{ data: submission.answers }}
          options={{ readOnly: true }}
        />
      ) : (
        <p>
          The archived spec version v{submission.formSpecVersion} is no longer
          available from the content engine.
        </p>
      )}
    </>
  );
}
