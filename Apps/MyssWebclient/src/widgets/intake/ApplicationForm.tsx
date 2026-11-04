import type { FormType } from "@formio/react/lib/components/Form";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  DraftApplicationForm,
  StatusPill,
  type DraftFormMessages,
} from "@myss/web-common/components";
import { ReadOnlySpecForm } from "@myss/formio";

import "@formio/js/dist/formio.form.min.css";
import "@/widgets/FormSpecWidget.css";
import styles from "@myss/web-common/components/ApplicationForm.module.css";

import {
  ApplicationRequestError,
  INTAKE_KEYWORDS,
  isEditableStatus,
  useApplication,
  useSaveAnswers,
  useSubmitApplication,
  type ApplicationPayload,
} from "@/hooks/useIntake";

// One Income Assistance application. While it is a draft, or once a worker
// has returned it for more information, it renders in the shared
// DraftApplicationForm (Save outside the Form.io renderer, Submit inside it).
// Otherwise the same spec renders read-only under its status, from the
// answers the event log holds.
//
// Design: docs/development_design/income-assistance-application.md.

const DRAFT_MESSAGES: DraftFormMessages = {
  conflict:
    "Another tab saved a newer version of this application, so the answers here were not saved. The latest version has been loaded; check your answers and save again to keep these.",
  notEditable:
    "This application has been submitted and cannot be changed while it is being reviewed.",
  returned:
    "We need more information to review this application. Update your answers, then submit the application again.",
  saveFailed: "Your answers could not be saved:",
  savedHint: "You can come back and finish this application later.",
  saveLabel: "Save",
};

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

/**
 * An application the applicant cannot change: just after a submit, the
 * acknowledgement; on a later visit, its status. The read-only form follows.
 */
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
  const waiting =
    application.status === "SUBMITTED" || application.status === "RESUBMITTED";
  const heading =
    application.status === "RESUBMITTED"
      ? "Application resubmitted"
      : waiting
        ? "Application submitted"
        : "Application status";

  return (
    <>
      <section
        className={waiting ? styles.outcome : styles.status}
        aria-live="polite"
      >
        <h2 ref={headingRef} tabIndex={-1} className={styles.heading}>
          {heading}
        </h2>
        {waiting ? (
          <p>
            Your{" "}
            {application.formSpecId === "application-for-assistance"
              ? "Application for Assistance"
              : "Income Assistance application"}{" "}
            has been received
            {submittedAt ? `. It was first submitted on ${submittedAt}` : ""}.
            It cannot be changed while it is being reviewed; if we need more
            information, it will be returned to you to update. Keep the request
            number below for your records.
          </p>
        ) : (
          <p>
            Your{" "}
            {application.formSpecId === "application-for-assistance"
              ? "Application for Assistance"
              : "Income Assistance application"}
            {submittedAt ? `, submitted on ${submittedAt},` : ""} cannot be
            changed. Its status is shown below.
          </p>
        )}
        <dl className={styles.details}>
          <dt>Request number</dt>
          <dd>
            <code>{application.referenceNumber}</code>
          </dd>
          <dt>Status</dt>
          <dd>
            <StatusPill status={application.status} />
          </dd>
        </dl>
      </section>
      <ReadOnlySpecForm spec={spec} answers={application.answers} />
    </>
  );
}

/** A draft or a returned application, saved and submitted as the applicant. */
function DraftForm({
  application,
  spec,
  onSubmitted,
}: Readonly<{
  application: ApplicationPayload;
  spec: FormType;
  onSubmitted: () => void;
}>) {
  const afa = application.formSpecId === "application-for-assistance";
  const save = useSaveAnswers(application.id, afa);
  const submit = useSubmitApplication(application.id, afa);

  return (
    <DraftApplicationForm
      application={application}
      spec={spec}
      save={save}
      submit={submit}
      onSubmitted={onSubmitted}
      notEditableKeyword={INTAKE_KEYWORDS.notEditable}
      messages={DRAFT_MESSAGES}
    />
  );
}

/**
 * Renders one application by id: editable while it is a draft or returned
 * for more information, read-only otherwise, with an acknowledgement right
 * after a submit. The spec comes with the application (the archived version
 * it was started on), so no separate spec fetch.
 */
export default function ApplicationForm({
  applicationId,
  expectedFormSpecId,
}: Readonly<{
  applicationId: string;
  expectedFormSpecId?: string;
}>) {
  const {
    data: application,
    error,
    isPending,
  } = useApplication(
    applicationId,
    expectedFormSpecId === "application-for-assistance",
  );
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

  if (expectedFormSpecId && application.formSpecId !== expectedFormSpecId) {
    return <p role="alert">This service request could not be found.</p>;
  }

  if (!application.spec) {
    return (
      <p role="alert">
        The form version v{application.formSpecVersion} this application uses is
        no longer available from the content engine.
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
