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
import { Link } from "react-router";
import {
  ApplicationRequestError,
  SubmissionRejectedError,
  type FormValidationError,
} from "@myss/web-common/api";
import {
  StatusPill,
  SubmissionErrors,
  useClientValidation,
} from "@myss/web-common/components";
import { ReadOnlySpecForm } from "@myss/formio";

import "@formio/js/dist/formio.form.min.css";
import styles from "./WorkerOverrideForm.module.css";

import {
  isEditableStatus,
  useSaveWorkerOverride,
  useSubmitWorkerOverride,
  useWorkerOverride,
  WORKER_OVERRIDE_KEYWORDS,
  type WorkerOverrideApplicationPayload,
} from "@/hooks/useWorkerOverride";
import { applicationPath } from "@/routes/paths";

// One worker override Application for Assistance (MYSS-321): the client's six
// details, filled in by a worker for a client who cannot submit their own.
// While it is a draft, or once returned for more information, the form renders
// editable with Save outside the renderer (the spec's own button is Submit,
// which runs Form.io's client validation and would block a partial save).
// Once submitted it renders read-only, with the request number and a link to
// it in the review queue. Any worker may open any draft.
//
// The draft-form handling follows the citizen webclient's
// widgets/intake/ApplicationForm.

const MESSAGE_CONFLICT =
  "Another worker, tab or window saved a newer version of this application, so the answers here were not saved. The latest version has been loaded; check the answers and save again to keep these.";
const MESSAGE_NOT_EDITABLE =
  "This application has been submitted and cannot be changed while it is being reviewed.";
const MESSAGE_RETURNED =
  "A worker asked for more information on this application. Update the answers, then submit it again.";

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
 * A submitted application: just after the submit, the acknowledgement takes
 * focus so a screen-reader user hears the outcome. The read-only form follows.
 */
function SubmittedView({
  application,
  spec,
  announce,
}: Readonly<{
  application: WorkerOverrideApplicationPayload;
  spec: FormType;
  announce: boolean;
}>) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  useEffect(() => {
    if (announce) headingRef.current?.focus();
  }, [announce]);
  const submittedAt = application.submittedAt
    ? new Date(application.submittedAt).toLocaleString()
    : undefined;

  return (
    <>
      <section className={styles.outcome} aria-live="polite">
        <h2 ref={headingRef} tabIndex={-1} className={styles.heading}>
          {application.status === "RESUBMITTED"
            ? "Application resubmitted"
            : "Application submitted"}
        </h2>
        <p>
          The worker override account has been created and the application is
          in the review queue
          {submittedAt ? `. It was first submitted on ${submittedAt}` : ""}.
        </p>
        <dl className={styles.details}>
          <dt>Request number</dt>
          <dd>
            <code>{application.referenceNumber}</code>
          </dd>
          <dt>Status</dt>
          <dd>
            <StatusPill status={application.status} />
          </dd>
          {application.accountId && (
            <>
              <dt>Account</dt>
              <dd>
                <code>{application.accountId}</code>
              </dd>
            </>
          )}
        </dl>
        <p>
          <Link to={applicationPath(application.id)}>
            Open it in the review queue
          </Link>
        </p>
      </section>
      <ReadOnlySpecForm spec={spec} answers={application.answers} />
    </>
  );
}

/**
 * A draft or a returned application: the editable form, Save outside the
 * renderer, Submit inside it.
 */
function DraftForm({
  application,
  spec,
  onSubmitted,
}: Readonly<{
  application: WorkerOverrideApplicationPayload;
  spec: FormType;
  onSubmitted: () => void;
}>) {
  const save = useSaveWorkerOverride(application.id);
  const submit = useSubmitWorkerOverride(application.id);
  const { mutate: saveAnswers } = save;
  const { mutate: submitApplication } = submit;

  // The answers as first loaded, and nothing later: a refetch after a save
  // returns what was just saved, and pushing that back into Form.io would
  // discard anything typed since the click.
  const [initialSubmission] = useState(() => ({ data: application.answers }));
  const formOptions = useMemo(() => ({ noAlerts: true }), []);
  // A submit Form.io blocks is listed in the same summary as one the API
  // refuses (Form.io's own alert is off).
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

  // A server error stays on a field until it is edited. Form.io counts
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
          // summary shown above the form.
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
    conflict?.keyword === WORKER_OVERRIDE_KEYWORDS.notEditable;
  const otherSaveError =
    save.error instanceof ApplicationRequestError && save.error.isConflict
      ? null
      : save.error;

  return (
    <section className={styles.formHost} onInputCapture={handleInputCapture}>
      {application.status === "RETURNED" && (
        <p className={styles.notice}>{MESSAGE_RETURNED}</p>
      )}
      {conflict && (
        <p role="alert" className={styles.notice}>
          {notEditable ? MESSAGE_NOT_EDITABLE : MESSAGE_CONFLICT}
        </p>
      )}
      {otherSaveError && (
        <p role="alert" className={styles.notice}>
          The answers could not be saved: {otherSaveError.message}
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
          . Any worker can open this draft from its link and finish it later.
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
          Save draft
        </Button>
      </div>
    </section>
  );
}

/**
 * Renders one worker override application by id: editable while it is a draft
 * or returned for more information, read-only once submitted. The spec comes
 * with the application (the version it was started on).
 */
export default function WorkerOverrideForm({
  applicationId,
}: Readonly<{
  applicationId: string;
}>) {
  const {
    data: application,
    error,
    isPending,
  } = useWorkerOverride(applicationId);
  // The acknowledgement takes focus only when the submit happened here; a
  // later visit just renders it.
  const [submittedHere, setSubmittedHere] = useState(false);
  const handleSubmitted = useCallback(() => setSubmittedHere(true), []);

  if (isPending) return <p>Loading application…</p>;
  if (error) {
    return (
      <p role="alert">
        {error instanceof ApplicationRequestError && error.status === 404
          ? "This worker override application could not be found."
          : `Could not load the application: ${error.message}`}
      </p>
    );
  }

  if (!application.spec) {
    return (
      <p role="alert">
        The form version v{application.formSpecVersion} this application uses
        is no longer available from the content engine.
      </p>
    );
  }

  if (!isEditableStatus(application.status)) {
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
      // A return remounts the form, so it starts from the answers as loaded.
      key={`${application.id}:${application.status}`}
      application={application}
      spec={application.spec.spec}
      onSubmitted={handleSubmitted}
    />
  );
}
