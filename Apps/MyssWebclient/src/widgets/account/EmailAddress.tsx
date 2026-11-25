import {
  Button,
  InlineAlert,
  TextField,
} from "@bcgov/design-system-react-components";
import { useEffect } from "react";

import {
  useCancelEmailChange,
  useResendEmailVerification,
  type AccountPayload,
} from "@/hooks/useAccount";
import { useFeature } from "@/hooks/useFeatures";
import styles from "./EmailAddress.module.css";
import { refusal } from "./refusal";

// The email address on Account Info (MYSS-305). A change, made through
// Contact Information's edit form, does not take effect at once: the new
// address is emailed a link and waits as pending, and the address in use
// stays until the link is followed. An address from registration is in use
// but unverified until its own link is followed. Both waiting states offer
// Resend link; a pending change can also be cancelled. All of it is behind
// the EmailVerification flag: off, the address is shown read-only, as it was
// before the feature.

export default function EmailAddress({
  account,
  isEditing,
}: Readonly<{
  account: AccountPayload;
  /** Contact Information's edit form is open; Resend and Cancel wait. */
  isEditing: boolean;
}>) {
  const resend = useResendEmailVerification();
  const cancel = useCancelEmailChange();
  const resetResend = resend.reset;
  const resetCancel = cancel.reset;

  // An earlier resend or cancel says nothing about what the edit will do.
  useEffect(() => {
    if (!isEditing) return;
    resetResend();
    resetCancel();
  }, [isEditing, resetResend, resetCancel]);

  const enabled = useFeature("EmailVerification") === true;
  const pending = account.pendingEmail ?? null;
  const unverified = !account.emailVerified && pending === null;

  const resendError = resend.error
    ? refusal(resend.error, "We could not send the link.")
    : null;
  const cancelError = cancel.error
    ? refusal(cancel.error, "Your change was not cancelled.")
    : null;
  const waitingAction = resend.isPending || cancel.isPending;

  if (!enabled) {
    return (
      <div className={styles.email}>
        <TextField label="Email Address" value={account.email} isReadOnly />
      </div>
    );
  }

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
    </div>
  );
}
