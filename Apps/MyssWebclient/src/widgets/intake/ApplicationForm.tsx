import { Button } from "@bcgov/design-system-react-components";
import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import type { Webform } from "@formio/js";
import {
  type InputEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

import "@formio/js/dist/formio.form.min.css";
import "@/widgets/FormSpecWidget.css";
import styles from "./ApplicationForm.module.css";

import { SubmissionRejectedError, type FormValidationError } from "@/api/forms";
import SubmissionErrors from "@/components/SubmissionErrors";
import { useClientValidation } from "@/hooks/useClientValidation";
import ReadOnlySpecForm from "@/widgets/ReadOnlySpecForm";
import {
  ApplicationRequestError,
  INTAKE_KEYWORDS,
  useApplication,
  useSaveAnswers,
  useSubmitApplication,
  type ApplicationPayload,
} from "@/hooks/useIntake";

// One Income Assistance application. While it is a draft the Form.io spec
// renders editable with a Save button OUTSIDE the renderer (the spec's own
// button is Submit, and pressing it runs Form.io's client validation, which
// would block a partial save). Once submitted the same spec renders read-only
// under an acknowledgement heading, from the answers the event log holds.
//
// Design: docs/development_design/income-assistance-application.md.

const MESSAGE_CONFLICT =
  "Another tab saved a newer version of this application, so the answers here were not saved. The latest version has been loaded; check your answers and save again to keep these.";
const MESSAGE_NOT_EDITABLE =
  "This application has already been submitted and can no longer be changed.";

interface FormSubmission {
  data: Record<string, unknown>;
}

/** The parts of the Form.io instance this widget reads beyond the typed API. */
interface FormData {
  submission?: { data?: Record<string, unknown> };
}

function formFieldName(target: EventTarget) {
  if (!(target instanceof HTMLElement)) return;

  const match = /^data\[([^\]]+)\]$/.exec(target.getAttribute("name") ?? "");
  return match?.[1];
}

/**
 * Moves focus to the acknowledgement heading when it appears, so a
 * screen-reader user is told the outcome rather than being left at the
 * submit button.
 */
function useFocusOnMount(enabled: boolean) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    if (enabled) headingRef.current?.focus();
  }, [enabled]);
  return headingRef;
}

/** A submitted application: the acknowledgement plus the read-only form. */
function SubmittedView({
  application,
  spec,
  announce,
}: Readonly<{
  application: ApplicationPayload;
  spec: FormType;
  announce: boolean;
}>) {
  const headingRef = useFocusOnMount(announce);
  const submittedAt = application.submittedAt
    ? new Date(application.submittedAt).toLocaleString()
    : undefined;

  return (
    <>
      <section className={styles.outcome} aria-live="polite">
        <h2 ref={headingRef} tabIndex={-1} className={styles.heading}>
          Application submitted
        </h2>
        <p>
          Your Income Assistance application has been received
          {submittedAt ? ` on ${submittedAt}` : ""}. It can no longer be
          changed. Keep the request number below for your records.
        </p>
        <dl className={styles.details}>
          <dt>Request number</dt>
          <dd>
            <code>{application.referenceNumber}</code>
          </dd>
        </dl>
      </section>
      <ReadOnlySpecForm spec={spec} answers={application.answers} />
    </>
  );
}

/** A draft: the editable form, Save outside the renderer, Submit inside it. */
function DraftForm({
  application,
  spec,
  onSubmitted,
}: Readonly<{
  application: ApplicationPayload;
  spec: FormType;
  onSubmitted: () => void;
}>) {
  const save = useSaveAnswers(application.id);
  const submit = useSubmitApplication(application.id);
  const { mutate: saveAnswers } = save;
  const { mutate: submitApplication } = submit;

  // The answers as first loaded, and nothing later: a refetch after a save
  // returns what was just saved, and pushing that back into Form.io would
  // discard anything typed since the click.
  const [initialSubmission] = useState(() => ({ data: application.answers }));
  const formOptions = useMemo(() => ({ noAlerts: true }), []);
  // A submit Form.io blocks is listed in the same summary as one the API
  // refuses (Form.io's own alert is already off).
  const clientValidation = useClientValidation();
  const { clear: clearClientValidation } = clientValidation;
  const formInstanceRef = useRef<Webform>(null);
  const displayedErrorRef = useRef<Error>(null);
  const validationErrors = useMemo(
    () =>
      submit.error instanceof SubmissionRejectedError
        ? submit.error.errors
        : [],
    [submit.error],
  );

  const handleFormReady = useCallback((instance: Webform) => {
    formInstanceRef.current = instance;
  }, []);

  // A server error stays on a field until the user edits it. Form.io counts
  // outstanding server errors as invalid, so without this the next submit
  // would be refused client-side with the stale message still showing.
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

  const currentAnswers = useCallback((): Record<string, unknown> => {
    const instance = formInstanceRef.current as FormData | null;
    return instance?.submission?.data ?? {};
  }, []);

  const handleSave = useCallback(() => {
    if (save.isPending || submit.isPending) return;
    saveAnswers({ version: application.version, answers: currentAnswers() });
  }, [
    application.version,
    currentAnswers,
    save.isPending,
    saveAnswers,
    submit.isPending,
  ]);

  const handleSubmit = useCallback(
    (submission: FormSubmission) => {
      if (submit.isPending) return;
      clearClientValidation();
      submitApplication(
        { version: application.version, answers: submission.data },
        {
          onSuccess: onSubmitted,
          // `cancelSubmit` re-enables Form.io's button without its own
          // "please fix the errors" message, which would contradict the
          // notice shown above the form.
          onError: () => formInstanceRef.current?.emit("cancelSubmit"),
        },
      );
    },
    [
      application.version,
      clearClientValidation,
      onSubmitted,
      submit.isPending,
      submitApplication,
    ],
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

    const formioErrors = validationErrors.map(
      (validationError: FormValidationError) => ({
        level: "error",
        message: validationError.message,
        path: validationError.field,
      }),
    );

    form.clearServerErrors();
    form.setServerErrors({ details: formioErrors });
    form.showErrors(formioErrors, false);
    form.emit("cancelSubmit");

    const first = validationErrors[0]?.field;
    if (first) form.focusOnComponent(first);
  }, [submit.error, validationErrors]);

  const conflict =
    (save.error instanceof ApplicationRequestError && save.error.isConflict
      ? save.error
      : null) ??
    (submit.error instanceof ApplicationRequestError && submit.error.isConflict
      ? submit.error
      : null);
  const notEditable =
    conflict?.keyword === INTAKE_KEYWORDS.notEditable ||
    (save.error instanceof ApplicationRequestError &&
      save.error.keyword === INTAKE_KEYWORDS.notEditable);
  const otherSaveError =
    save.error instanceof ApplicationRequestError && save.error.isConflict
      ? null
      : save.error;

  return (
    <section className={styles.formHost} onInputCapture={handleInputCapture}>
      {conflict && (
        <p role="alert" className={styles.notice}>
          {notEditable ? MESSAGE_NOT_EDITABLE : MESSAGE_CONFLICT}
        </p>
      )}
      {otherSaveError && (
        <p role="alert" className={styles.notice}>
          Your answers could not be saved: {otherSaveError.message}
        </p>
      )}
      {clientValidation.error ? (
        <SubmissionErrors error={clientValidation.error} />
      ) : (
        submit.error && !conflict && <SubmissionErrors error={submit.error} />
      )}
      {save.isSuccess && !save.isPending && (
        <output className={styles.saved}>
          Saved{" "}
          {save.data.updatedAt
            ? `at ${new Date(save.data.updatedAt).toLocaleTimeString()}`
            : ""}
          . You can come back and finish this application later.
        </output>
      )}
      <Form
        src={spec}
        submission={initialSubmission}
        options={formOptions}
        onFormReady={handleFormReady}
        onSubmitError={clientValidation.onSubmitError}
        onSubmit={handleSubmit}
      />
      <div className={styles.actions}>
        <Button
          variant="secondary"
          onPress={handleSave}
          isDisabled={save.isPending || submit.isPending}
        >
          Save
        </Button>
      </div>
    </section>
  );
}

/**
 * Renders one application by id: editable while it is a draft, read-only
 * with an acknowledgement once submitted. The spec comes with the application
 * (the archived version it was started on), so no separate spec fetch.
 */
export default function ApplicationForm({
  applicationId,
}: Readonly<{
  applicationId: string;
}>) {
  const { data: application, error, isPending } = useApplication(applicationId);
  // The acknowledgement heading takes focus only when the submit happened in
  // this session; a later visit to a submitted application just renders it.
  const [submittedHere, setSubmittedHere] = useState(false);
  const handleSubmitted = useCallback(() => setSubmittedHere(true), []);

  if (isPending) return <p>Loading application…</p>;
  if (error) {
    return (
      <p role="alert">
        {error instanceof ApplicationRequestError && error.status === 404
          ? "This application could not be found."
          : `Could not load the application: ${error.message}`}
      </p>
    );
  }

  if (!application.spec) {
    return (
      <p role="alert">
        The form version v{application.formSpecVersion} this application uses is
        no longer available from the content engine.
      </p>
    );
  }

  if (application.status === "SUBMITTED") {
    return (
      <SubmittedView
        application={application}
        spec={application.spec.spec}
        announce={submittedHere}
      />
    );
  }

  return (
    <DraftForm
      key={application.id}
      application={application}
      spec={application.spec.spec}
      onSubmitted={handleSubmitted}
    />
  );
}
