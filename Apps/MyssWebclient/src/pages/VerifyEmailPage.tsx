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

function tokenFrom(hash: string): string | null {
  return new URLSearchParams(hash.replace(/^#/, "")).get("token");
}

export default function VerifyEmailPage() {
  const { hash } = useLocation();
  const verify = useVerifyEmail();
  const token = tokenFrom(hash);

  const refusedLink =
    verify.error instanceof AccountRequestError &&
    verify.error.keyword === EMAIL_LINK_INVALID_KEYWORD
      ? verify.error
      : null;
  const generalError = verify.error && !refusedLink ? verify.error : null;

  let content: React.ReactNode;
  if (verify.isSuccess) {
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
          Confirm that this is your email address for your My Self Serve
          account.
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
