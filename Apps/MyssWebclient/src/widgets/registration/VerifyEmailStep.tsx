import { Button, InlineAlert } from "@bcgov/design-system-react-components";
import { useEffect, useRef, useState } from "react";

import {
  AccountRequestError,
  EMAIL_TOO_SOON_KEYWORD,
  useRegistrationEmail,
  useRequestRegistrationEmail,
} from "@/hooks/useAccount";
import { isEmailAddress } from "@myss/formio";
import styles from "./VerifyEmailStep.module.css";

// Registration's Step 2 (MYSS-295). A link went to the address from Step 1;
// the citizen follows it (usually in another tab, or on their phone), comes
// back, and selects Continue. Continue asks the API afresh, so a verified
// address is never taken on the browser's word. The status is also read again
// when the citizen returns to this tab, so it can say "verified" before they
// press anything.

function sameAddress(a?: string | null, b?: string | null): boolean {
  return (a ?? "").trim().toLowerCase() === (b ?? "").trim().toLowerCase();
}

export default function VerifyEmailStep({
  email,
  subject,
  onBack,
  onVerified,
}: Readonly<{
  /** The address from Step 1, the one the link was sent to. */
  email: string;
  /** The signed-in subject, which scopes the cached status. */
  subject: string;
  onBack: () => void;
  onVerified: () => void;
}>) {
  const status = useRegistrationEmail(subject, isEmailAddress(email));
  const resend = useRequestRegistrationEmail(subject);
  const [notYet, setNotYet] = useState(false);
  const [checking, setChecking] = useState(false);
  const headingRef = useRef<HTMLHeadingElement>(null);

  // Arriving here is a new step: say so to a screen-reader user.
  useEffect(() => {
    headingRef.current?.focus();
  }, []);

  const verified =
    status.data?.verified === true && sameAddress(status.data.email, email);

  async function continueIfVerified() {
    setChecking(true);
    try {
      const { data } = await status.refetch();
      if (data?.verified && sameAddress(data.email, email)) {
        onVerified();
        return;
      }
      setNotYet(true);
    } finally {
      setChecking(false);
    }
  }

  // A refused resend inside the cooldown is a wait, not a failure.
  const resendError = resend.error;
  const resendWait =
    resendError instanceof AccountRequestError &&
    resendError.keyword === EMAIL_TOO_SOON_KEYWORD;

  return (
    <section aria-labelledby="verify-email-heading" className={styles.step}>
      <h2 id="verify-email-heading" ref={headingRef} tabIndex={-1}>
        Step 2: Verify your email
      </h2>

      {verified ? (
        <output>
          <InlineAlert
            variant="success"
            description={`${email} is verified. Select Continue to review and submit.`}
          />
        </output>
      ) : (
        <>
          <p>
            We sent an email to <strong>{email}</strong> with a link to verify
            your email address.
          </p>
          <p>
            Open the link in the email, then come back to this page and select
            Continue. If the email does not arrive within a few minutes, check
            your junk folder or resend the link.
          </p>
        </>
      )}

      {notYet && !verified && (
        <InlineAlert
          variant="danger"
          role="alert"
          title="Email verification is required"
          description={`Verify ${email} before you continue. Open the link in the email we sent, then select Continue.`}
        />
      )}

      {resend.isSuccess && !verified && (
        <output>
          <InlineAlert
            variant="success"
            description="We sent a new link. Earlier links no longer work."
          />
        </output>
      )}
      {resendError && (
        <InlineAlert
          variant={resendWait ? "warning" : "danger"}
          role="alert"
          description={
            resendWait
              ? resendError.message
              : `We could not send the link. ${resendError.message}`
          }
        />
      )}

      <div className={styles.actions}>
        <Button variant="secondary" onPress={onBack}>
          Back
        </Button>
        {!verified && (
          <Button
            variant="secondary"
            onPress={() => resend.mutate(email)}
            isDisabled={resend.isPending}
          >
            Resend link
          </Button>
        )}
        <Button
          variant="primary"
          onPress={() => void continueIfVerified()}
          isDisabled={checking}
        >
          Continue
        </Button>
      </div>
    </section>
  );
}
