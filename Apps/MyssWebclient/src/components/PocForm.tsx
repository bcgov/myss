import { Form } from "@formio/react";
import { useEffect, useRef } from "react";
import { Link } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./PocForm.css";

import {
  SubmissionRejectedError,
  useFormSpec,
  useSubmitForm,
} from "@/hooks/usePocForm";

const FORM_SPEC_ID = "poc-test-form";

/**
 * Moves focus to the input a validation error belongs to.
 *
 * Form.io names its inputs `data[<key>]`, and the API reports failures by
 * component key, so the two line up without the client needing to know
 * anything about Form.io's generated element ids (which are random per render
 * and therefore useless as anchor targets).
 *
 * A miss is deliberately silent: the message is already on screen, and a field
 * that cannot be focused — hidden by a conditional, say — is not worth
 * throwing over.
 */
function focusField(field: string) {
  const input = document.querySelector<HTMLElement>(`[name="data[${CSS.escape(field)}]"]`);
  if (!input) return;

  input.focus();
  input.scrollIntoView({ block: "center", behavior: "smooth" });
}

/**
 * The error summary for a refused submission.
 *
 * Rendered as a list of every reason at once rather than one at a time,
 * because that is the shape the 422 body is deliberately built in. Focus moves
 * to the heading when it appears, so a screen-reader user is told the
 * submission failed instead of being left at the submit button in silence.
 */
function SubmissionErrors({ error }: { error: Error }) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  const errors = error instanceof SubmissionRejectedError ? error.errors : [];

  useEffect(() => {
    headingRef.current?.focus();
  }, [error]);

  return (
    <div className="poc-form-errors" role="alert">
      <h4 ref={headingRef} tabIndex={-1}>
        There is a problem
      </h4>
      {errors.length === 0 ? (
        // Non-422 failures (401, 500, an HTML error page from a proxy) carry no
        // error collection; the thrown message is all there is to show.
        <p>{error.message}</p>
      ) : (
        <ul>
          {errors.map((validationError) => (
            <li key={`${validationError.field}:${validationError.keyword}`}>
              <button type="button" onClick={() => focusField(validationError.field)}>
                {validationError.message}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

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
