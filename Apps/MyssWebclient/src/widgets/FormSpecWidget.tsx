import { Form } from "@formio/react";
import type { Webform } from "@formio/js";
import {
  type InputEvent,
  type ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useRef,
} from "react";
import { Link } from "react-router";

import "@formio/js/dist/formio.form.min.css";
import "./FormSpecWidget.css";

import { SubmissionRejectedError, type FormValidationError } from "@/api/forms";
import SubmissionErrors from "@/components/SubmissionErrors";
import { useClientValidation } from "@/hooks/useClientValidation";
import { useFormSpec, useSubmitForm } from "@/hooks/usePocForm";

interface FormSpecWidgetProps {
  formSpecId: string;
  renderSubmissionError?: (error: Error) => ReactNode;
  showSpecHeading?: boolean;
}

interface FormSubmission {
  data: Record<string, unknown>;
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

export default function FormSpecWidget({
  formSpecId,
  renderSubmissionError,
  showSpecHeading = false,
}: Readonly<FormSpecWidgetProps>) {
  const { data: spec, error, isPending } = useFormSpec(formSpecId);
  const submit = useSubmitForm(formSpecId);
  const { mutate: submitForm } = submit;
  // A submit Form.io blocks is listed in the same summary as one the API
  // refuses; Form.io's own alert is switched off so nothing shows twice.
  const clientValidation = useClientValidation();
  const { clear: clearClientValidation } = clientValidation;
  const formElementRef = useRef<HTMLElement>(null);
  const formInstanceRef = useRef<Webform>(null);
  const displayedErrorRef = useRef<Error>(null);
  const formOptions = useMemo(() => ({ noAlerts: true }), []);
  const validationErrors =
    submit.error instanceof SubmissionRejectedError ? submit.error.errors : [];
  const handleFormReady = useCallback((instance: Webform) => {
    formInstanceRef.current = instance;
  }, []);
  const handleInputCapture = useCallback((event: InputEvent<HTMLElement>) => {
    const field = formFieldName(event.target);
    const form = formInstanceRef.current;
    const displayedError = displayedErrorRef.current;
    if (
      !field ||
      !form ||
      !(displayedError instanceof SubmissionRejectedError) ||
      !displayedError.errors.some(
        (validationError) => validationError.field === field,
      )
    ) {
      return;
    }

    form.serverErrors = (form.serverErrors ?? []).filter(
      (serverError: { path?: string }) => serverError.path !== field,
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

      clearClientValidation();
      submitForm({
        formSpecVersion: spec.version,
        answers: submission.data,
      });
    },
    [clearClientValidation, spec, submitForm],
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

    // The API's wording is the authored and catalogue wording already; it is
    // shown as it arrives.
    const formioErrors = validationErrors.map((validationError) => ({
      level: "error",
      message: validationError.message,
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
      {showSpecHeading && (
        <h3>
          {spec.title ?? spec.formSpecId} <small>(spec v{spec.version})</small>
        </h3>
      )}
      {clientValidation.error ? (
        <SubmissionErrors error={clientValidation.error} />
      ) : (
        submit.error &&
        (renderSubmissionError ? (
          renderSubmissionError(submit.error)
        ) : (
          <p role="alert">Submission failed: {submit.error.message}</p>
        ))
      )}
      <Form
        src={spec.spec}
        options={formOptions}
        onFormReady={handleFormReady}
        onSubmitError={clientValidation.onSubmitError}
        onSubmit={handleSubmit}
      />
    </section>
  );
}
