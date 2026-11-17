import { Button, InlineAlert } from "@bcgov/design-system-react-components";
import { Link, useLocation } from "react-router";

import {
  AccountRequestError,
  EMAIL_LINK_INVALID_KEYWORD,
  useVerifyEmail,
} from "@/hooks/useAccount";
import { paths } from "@/routes/paths";
import styles from "./PinResetPage.module.css";

// The page the emailed verification link opens (MYSS-305). Public: the token
// in the link is the credential, so it works on whatever device opens the
// email. The token arrives in the URL fragment (#token=…), which the browser
// never sends to a server. Confirming takes a press, not just the page load:
// mail scanners open links to check them, and must not use the link up.
//
// A registration's link (MYSS-295) is marked ?for=registration. It opens in a
// new window while the half-filled registration waits in the first, so this
// page tells the citizen to close it and go back there, and links nowhere.
// The mark only chooses the wording; the token alone decides what is verified.

function tokenFrom(hash: string): string | null {
  return new URLSearchParams(hash.replace(/^#/, "")).get("token");
}

/** Marks a registration's link (EmailVerificationConfig.RegistrationLinkPath). */
const REGISTRATION = "registration";

export default function VerifyEmailPage() {
  const { hash, search } = useLocation();
  const verify = useVerifyEmail();
  const token = tokenFrom(hash);
  const forRegistration =
    new URLSearchParams(search).get("for") === REGISTRATION;

  const refusedLink =
    verify.error instanceof AccountRequestError &&
    verify.error.keyword === EMAIL_LINK_INVALID_KEYWORD
      ? verify.error
      : null;
  const generalError = verify.error && !refusedLink ? verify.error : null;

  let content: React.ReactNode;
  if (verify.isSuccess && forRegistration) {
    content = (
      <>
        <output>
          <InlineAlert
            variant="success"
            title="Your email address is verified"
            description="You can close this window. Go back to the window where you are creating your MySS account and select Continue."
          />
        </output>
      </>
    );
  } else if (verify.isSuccess) {
    content = (
      <>
        <output>
          <InlineAlert
            variant="success"
            description="Your email address is confirmed. We will use it for messages about your account."
          />
        </output>
        <p>
          <Link to={paths.accountInfo}>Go to Account Info</Link>
        </p>
      </>
    );
  } else if ((!token || refusedLink) && forRegistration) {
    content = (
      <InlineAlert
        variant="danger"
        role="alert"
        title="This link no longer works"
        description="The link has expired, has already been used, or a newer one was sent. Go back to the window where you are creating your MySS account and choose Resend link."
      />
    );
  } else if (!token || refusedLink) {
    content = (
      <>
        <InlineAlert
          variant="danger"
          role="alert"
          title="This link no longer works"
          description={
            refusedLink?.message ??
            "This link is not complete. Open the link from your email again, or sign in and choose Resend link on Account Info to get a new one."
          }
        />
        <p>
          <Link to={paths.signIn}>Sign in to My Self Serve</Link>
        </p>
      </>
    );
  } else {
    content = (
      <>
        <p>
          {forRegistration
            ? "Confirm that this is your email address, then go back to the window where you are creating your MySS account."
            : "Confirm that this is your email address for your My Self Serve account."}
        </p>
        {generalError && (
          <InlineAlert
            variant="danger"
            role="alert"
            description={`Your email address was not confirmed. ${generalError.message}`}
          />
        )}
        <div>
          <Button
            variant="primary"
            onPress={() => verify.mutate(token)}
            isDisabled={verify.isPending}
          >
            Confirm my email address
          </Button>
        </div>
      </>
    );
  }

  return (
    <div className={styles.page}>
      <h1>Confirm your email address</h1>
      {content}
    </div>
  );
}
