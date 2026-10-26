import {
  Button,
  InlineAlert,
  TextField,
} from "@bcgov/design-system-react-components";
import { useEffect, useRef, useState } from "react";

import {
  AccountRequestError,
  EMAIL_TOO_SOON_KEYWORD,
  useCancelEmailChange,
  useChangeEmail,
  useResendEmailVerification,
  type AccountPayload,
} from "@/hooks/useAccount";
import {
  isEmailAddress,
  RULE_DEFAULT_MESSAGES,
  RULE_KEYWORDS,
} from "@myss/formio";
import { catalogueMessage } from "@myss/formio/errorCatalogue";
import styles from "./EmailAddress.module.css";

// The email address on Account Info (MYSS-305). A change does not take effect
// at once: the new address is emailed a link and waits as pending, and the
// address in use stays until the link is followed. An address from
// registration is in use but unverified until its own link is followed. Both
// waiting states offer Resend link; a pending change can also be cancelled.

const FORMAT_MESSAGE =
  catalogueMessage(RULE_KEYWORDS.emailInvalidFormat) ??
  RULE_DEFAULT_MESSAGES[RULE_KEYWORDS.emailInvalidFormat];

/** The message a refused call shows; a 429 is a wait, not a failure. */
function refusal(
  error: Error,
  failed: string,
): {
  variant: "warning" | "danger";
  text: string;
} {
  return error instanceof AccountRequestError &&
    error.keyword === EMAIL_TOO_SOON_KEYWORD
    ? { variant: "warning", text: error.message }
    : { variant: "danger", text: `${failed} ${error.message}` };
}

export default function EmailAddress({
  account,
}: Readonly<{ account: AccountPayload }>) {
  const change = useChangeEmail();
  const resend = useResendEmailVerification();
  const cancel = useCancelEmailChange();
  const [isEditing, setIsEditing] = useState(false);
  const [newEmail, setNewEmail] = useState("");
  const [fieldError, setFieldError] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);
  // BCDS Button does not forward a ref, so focus goes through its wrapper.
  const editActionRef = useRef<HTMLDivElement>(null);
  const focusAfterToggle = useRef<"form" | "editAction" | null>(null);

  // Focus moves once React has put the new elements on the page.
  useEffect(() => {
    const target = focusAfterToggle.current;
    focusAfterToggle.current = null;
    if (target === "form") {
      formRef.current?.querySelector("input")?.focus();
    } else if (target === "editAction") {
      editActionRef.current?.querySelector("button")?.focus();
    }
  }, [isEditing]);

  const pending = account.pendingEmail ?? null;
  const unverified = !account.emailVerified && pending === null;

  function open() {
    setNewEmail("");
    setFieldError(null);
    change.reset();
    resend.reset();
    cancel.reset();
    focusAfterToggle.current = "form";
    setIsEditing(true);
  }

  function close() {
    focusAfterToggle.current = "editAction";
    setIsEditing(false);
  }

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!isEmailAddress(newEmail)) {
      setFieldError(FORMAT_MESSAGE);
      return;
    }
    setFieldError(null);
    change.mutate(newEmail.trim(), {
      onSuccess: close,
      onError: (error) => {
        const emailError =
          error instanceof AccountRequestError
            ? error.errors.find((e) => e.field === "email")
            : undefined;
        if (emailError) setFieldError(emailError.message);
      },
    });
  }

  // A refusal of the change with no field error (429, 503, offline).
  const changeError =
    change.error &&
    !(
      change.error instanceof AccountRequestError &&
      change.error.errors.length > 0
    )
      ? refusal(change.error, "Your email address was not changed.")
      : null;
  const resendError = resend.error
    ? refusal(resend.error, "We could not send the link.")
    : null;
  const cancelError = cancel.error
    ? refusal(cancel.error, "Your change was not cancelled.")
    : null;
  const waitingAction = resend.isPending || cancel.isPending;

  return (
    <div className={styles.email}>
      <TextField label="Email Address" value={account.email} isReadOnly />

      {unverified && (
        <InlineAlert
          variant="warning"
          title="Email address not verified"
          description={`We sent a link to ${account.email}. Follow it to confirm this is your email address.`}
        />
      )}

      {pending !== null && (
        <InlineAlert
          variant="info"
          title="Verification pending"
          description={`We sent a link to ${pending}. Until you follow it, we will keep using ${account.email}.`}
        />
      )}

      {resend.isSuccess && (
        <output className={styles.confirmation}>
          <InlineAlert
            variant="success"
            description="We sent a new link. Earlier links no longer work."
          />
        </output>
      )}
      {resendError && (
        <InlineAlert
          variant={resendError.variant}
          role="alert"
          description={resendError.text}
        />
      )}
      {cancelError && (
        <InlineAlert
          variant={cancelError.variant}
          role="alert"
          description={cancelError.text}
        />
      )}

      {(unverified || pending !== null) && !isEditing && (
        <div className={styles.actions}>
          <Button
            variant="secondary"
            onPress={() => {
              cancel.reset();
              resend.mutate();
            }}
            isDisabled={waitingAction}
          >
            Resend link
          </Button>
          {pending !== null && (
            <Button
              variant="secondary"
              onPress={() => {
                resend.reset();
                cancel.mutate();
              }}
              isDisabled={waitingAction}
            >
              Cancel change
            </Button>
          )}
        </div>
      )}

      {isEditing ? (
        <form
          ref={formRef}
          className={styles.form}
          onSubmit={submit}
          noValidate
          aria-label="Change your email address"
        >
          <TextField
            label="New Email Address"
            name="email"
            type="email"
            autoComplete="email"
            isRequired
            validationBehavior="aria"
            value={newEmail}
            onChange={setNewEmail}
            isInvalid={fieldError !== null}
            errorMessage={fieldError ?? undefined}
            description="We will email a link to this address. Your email address changes when you follow it."
          />

          {changeError && (
            <InlineAlert
              variant={changeError.variant}
              role="alert"
              description={changeError.text}
            />
          )}

          <div className={styles.actions}>
            <Button
              variant="primary"
              type="submit"
              isDisabled={change.isPending}
            >
              Save
            </Button>
            <Button
              variant="secondary"
              onPress={close}
              isDisabled={change.isPending}
            >
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        <div ref={editActionRef}>
          <Button variant="secondary" onPress={open}>
            Edit Email Address
          </Button>
        </div>
      )}
    </div>
  );
}
