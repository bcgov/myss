import { Form } from "@formio/react";
import type { Webform } from "@formio/js";
import { type FormEvent, useCallback, useEffect, useMemo, useRef } from "react";
import { Link } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./PocForm.css";

import { SubmissionRejectedError, type FormValidationError } from "@/api/forms";
import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";

const FORM_SPEC_ID = "bc-bus-pass";
const SIN_FIELD = "socialInsuranceNumber";
const SIN_ERROR_MESSAGE = "SIN must be valid";

interface FormSubmission {
  data: Record<string, unknown>;
}

function isSinError(error: FormValidationError) {
  return error.field === SIN_FIELD && error.keyword.startsWith("IDA.SIN.");
}

function busPassErrorMessage(error: FormValidationError) {
  return isSinError(error) ? SIN_ERROR_MESSAGE : error.message;
}

function firstErrorField(
  formElement: HTMLElement | null,
  errors: readonly FormValidationError[],
) {
  const fieldsByInputName = new Map(
    errors.map((error) => [`data[${error.field}]`, error.field]),
  );

  for (const input of formElement?.querySelectorAll<HTMLElement>("[name]") ??
    []) {
    const field = fieldsByInputName.get(input.getAttribute("name") ?? "");
    if (field) return field;
  }

  return errors[0]?.field;
}

function formFieldName(target: EventTarget) {
  if (!(target instanceof HTMLElement)) return;

  const match = /^data\[([^\]]+)\]$/.exec(target.getAttribute("name") ?? "");
  return match?.[1];
}

/**
 * Renders the BC Bus Pass Form.io spec fetched from the MyssApi forms endpoint
 * and posts the answers back with the exact spec version the form was rendered under.
 */
export default function BusPassForm() {
  const { data: spec, error, isPending } = useFormSpec(FORM_SPEC_ID);
  const submit = useSubmitForm(FORM_SPEC_ID);
  const { mutate: submitForm } = submit;
  const formElementRef = useRef<HTMLElement>(null);
  const formInstanceRef = useRef<Webform>(null);
  const displayedErrorRef = useRef<Error>(null);
  const formOptions = useMemo(() => ({ noAlerts: true }), []);
  const validationErrors =
    submit.error instanceof SubmissionRejectedError ? submit.error.errors : [];
  const handleFormReady = useCallback((instance: Webform) => {
    formInstanceRef.current = instance;
  }, []);
  const handleInputCapture = useCallback((event: FormEvent<HTMLElement>) => {
    const field = formFieldName(event.target);
    const form = formInstanceRef.current;
    const displayedError = displayedErrorRef.current;
    if (
      !field ||
      !form ||
      !(displayedError instanceof SubmissionRejectedError) ||
      !displayedError.errors.some((error) => error.field === field)
    ) {
      return;
    }

    form.serverErrors = (form.serverErrors ?? []).filter(
      (error: { path?: string }) => error.path !== field,
    );
    const component = form.getComponent(field);
    if (component) {
      component.serverErrors = [];
      component.setCustomValidity([], false, true);
    }

    if (form.serverErrors.length === 0) {
      displayedErrorRef.current = null;
    }
  }, []);
  const handleSubmit = useCallback(
    (submission: FormSubmission) => {
      if (spec === undefined) return;

      submitForm({
        formSpecVersion: spec.version,
        answers: submission.data,
      });
    },
    [spec, submitForm],
  );

  useEffect(() => {
    const form = formInstanceRef.current;
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
      message: busPassErrorMessage(validationError),
      path: validationError.field,
    }));

    form.clearServerErrors();
    form.setServerErrors({ details: formioErrors });
    form.showErrors(formioErrors, false);
    form.emit("cancelSubmit");

    const field = firstErrorField(formElementRef.current, validationErrors);
    if (field) form.focusOnComponent(field);
  }, [submit.error, validationErrors]);

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
    <section ref={formElementRef} onInputCapture={handleInputCapture}>
      {submit.error && validationErrors.length === 0 && (
        <p role="alert">Submission failed: {submit.error.message}</p>
      )}
      <Form
        src={spec.spec}
        options={formOptions}
        onFormReady={handleFormReady}
        onSubmit={handleSubmit}
      />
    </section>
  );
}
