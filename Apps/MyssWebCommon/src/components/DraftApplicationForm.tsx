import { Button } from "@bcgov/design-system-react-components";
import { Form } from "@formio/react";
import type {
  FormProps,
  FormType,
  JSON as FormioJson,
} from "@formio/react/lib/components/Form";
import type { UseMutationResult } from "@tanstack/react-query";
import {
  type InputEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

import { ApplicationRequestError } from "../api/applications";
import { SubmissionRejectedError, type FormValidationError } from "../api/forms";
import SubmissionErrors from "./SubmissionErrors";
import { useClientValidation } from "./useClientValidation";

import styles from "./ApplicationForm.module.css";

// The editable half of an intake application, shared by the citizen's
// ApplicationForm and the admin site's worker override form: the Form.io spec
// with Save OUTSIDE the renderer (the spec's own button is Submit, and pressing
// it runs Form.io's client validation, which would block a partial save), the
// API's field errors pinned onto the form, and the conflict, save and saved
// notices. Each app passes its own save/submit mutations and its wording; the
// read-only view after a submit stays in each app, where the two differ.

/** The Form.io instance, as @formio/react hands it to onFormReady. */
type Webform = Parameters<NonNullable<FormProps["onFormReady"]>>[0];

/** What a save or a submit sends: the row version last read and the answers. */
export interface DraftAnswersInput {
  version: number;
  answers: Record<string, unknown>;
}

/** The application fields the draft form reads. */
export interface DraftApplication {
  version: number;
  status: string;
  answers: { [key: string]: FormioJson };
}

/** The wording that differs between the citizen and the worker. */
export interface DraftFormMessages {
  /** Another tab or worker saved first (409). */
  conflict: string;
  /** Submitted and not returned, so it cannot change (409). */
  notEditable: string;
  /** Shown above the form while it is returned for more information. */
  returned: string;
  /** Leads the message of a save that failed for another reason. */
  saveFailed: string;
  /** Follows "Saved at …" after a save. */
  savedHint: string;
  /** The Save button's label. */
  saveLabel: string;
}

type DraftMutation<TData> = UseMutationResult<TData, Error, DraftAnswersInput>;

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

function conflictOf(error: Error | null) {
  return error instanceof ApplicationRequestError && error.isConflict
    ? error
    : null;
}

/**
 * A draft or a returned application: the editable form, Save outside the
 * renderer, Submit inside it. Mount it with a key that changes when the
 * application is returned, so it starts again from the answers as loaded.
 */
export default function DraftApplicationForm<
  TData extends { updatedAt?: string | null },
>({
  application,
  spec,
  save,
  submit,
  onSubmitted,
  notEditableKeyword,
  messages,
}: Readonly<{
  application: DraftApplication;
  spec: FormType;
  save: DraftMutation<TData>;
  submit: DraftMutation<TData>;
  onSubmitted: () => void;
  /** The keyword the API puts on a 409 for an application that cannot change. */
  notEditableKeyword: string;
  messages: DraftFormMessages;
}>) {
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

  const conflict = conflictOf(save.error) ?? conflictOf(submit.error);
  const notEditable =
    conflict?.keyword === notEditableKeyword ||
    (save.error instanceof ApplicationRequestError &&
      save.error.keyword === notEditableKeyword);
  const otherSaveError = conflictOf(save.error) ? null : save.error;
  const savedAt = save.data?.updatedAt;

  return (
    <section className={styles.formHost} onInputCapture={handleInputCapture}>
      {application.status === "RETURNED" && (
        <p className={styles.notice}>{messages.returned}</p>
      )}
      {conflict && (
        <p role="alert" className={styles.notice}>
          {notEditable ? messages.notEditable : messages.conflict}
        </p>
      )}
      {otherSaveError && (
        <p role="alert" className={styles.notice}>
          {messages.saveFailed} {otherSaveError.message}
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
          {savedAt ? `at ${new Date(savedAt).toLocaleTimeString()}` : ""}.{" "}
          {messages.savedHint}
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
          {messages.saveLabel}
        </Button>
      </div>
    </section>
  );
}
