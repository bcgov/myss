import { Button, InlineAlert } from "@bcgov/design-system-react-components";
import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router";

import PinField from "@/components/PinField";
import {
  AccountRequestError,
  PIN_RESET_LINK_INVALID_KEYWORD,
  useResetPin,
} from "@/hooks/useAccount";
import { checkNewPin } from "@/lib/pin";
import { paths } from "@/routes/paths";
import FieldErrorSummary from "@/widgets/account/FieldErrorSummary";
import styles from "./PinResetPage.module.css";

// The page the emailed PIN reset link opens (MYSS-36). Public: the token in
// the link is the credential, and the citizen signs in afterwards with the new
// PIN. The token arrives in the URL fragment (#token=…), which the browser
// never sends to a server, so it stays out of access logs and Referer headers.
// On success the citizen is sent to sign in, with a confirmation there.

type FieldErrors = Record<string, string>;

const FIELD_NAMES: Readonly<Record<string, string>> = {
  newPin: "New PIN",
  confirmPin: "Confirm New PIN",
};

/** What the sign-in page shows after a reset. */
export const PIN_RESET_NOTICE = "Your PIN has been reset. Sign in to continue.";

function fieldName(field: string): string {
  return FIELD_NAMES[field] ?? "PIN";
}

function tokenFrom(hash: string): string | null {
  return new URLSearchParams(hash.replace(/^#/, "")).get("token");
}

export default function PinResetPage() {
  const { hash } = useLocation();
  const navigate = useNavigate();
  const reset = useResetPin();
  const token = tokenFrom(hash);
  const [newPin, setNewPin] = useState("");
  const [confirmPin, setConfirmPin] = useState("");
  const [errors, setErrors] = useState<FieldErrors>({});
  const formRef = useRef<HTMLFormElement>(null);
  const summaryRef = useRef<HTMLHeadingElement>(null);

  // After a refused submit, focus the error summary so a screen-reader user
  // hears that it failed and can go from there to each field.
  useEffect(() => {
    if (Object.keys(errors).length === 0) return;
    summaryRef.current?.focus();
  }, [errors]);

  const refusedLink =
    reset.error instanceof AccountRequestError &&
    reset.error.keyword === PIN_RESET_LINK_INVALID_KEYWORD
      ? reset.error
      : null;
  const linkIsDead = !token || refusedLink !== null;

  function focusField(field: string) {
    formRef.current
      ?.querySelector(`[data-field="${CSS.escape(field)}"]`)
      ?.querySelector<HTMLElement>("input")
      ?.focus();
  }

  function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!token) return;
    const found = Object.fromEntries(
      checkNewPin(newPin, confirmPin, {
        pin: "newPin",
        confirmation: "confirmPin",
      }).map((error) => [error.field, error.message]),
    );
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    reset.mutate(
      { token, newPin, confirmPin },
      {
        onSuccess: () => {
          void navigate(paths.signIn, {
            replace: true,
            state: { notice: PIN_RESET_NOTICE },
          });
        },
        onError: (error) => {
          if (error instanceof AccountRequestError && error.errors.length > 0) {
            setErrors(
              Object.fromEntries(
                error.errors.map((fieldError) => [
                  fieldError.field,
                  fieldError.message,
                ]),
              ),
            );
          }
        },
      },
    );
  }

  // A refusal with no field errors and not about the link (500, offline).
  const generalError =
    reset.error &&
    !linkIsDead &&
    !(
      reset.error instanceof AccountRequestError &&
      reset.error.errors.length > 0
    )
      ? reset.error
      : null;

  return (
    <div className={styles.page}>
      <h1>Reset your PIN</h1>

      {linkIsDead ? (
        <>
          <InlineAlert
            variant="danger"
            role="alert"
            title="This link no longer works"
            description={
              refusedLink?.message ??
              "This link is not complete. Open the link from your email again, or sign in and choose Reset PIN on Account Info to get a new one."
            }
          />
          <p>
            <Link to={paths.signIn}>Sign in to My Self Serve</Link>
          </p>
        </>
      ) : (
        <>
          <p>
            Choose a new 4-digit number that will verify your identity when you
            sign and submit documents online.
          </p>
          <form
            ref={formRef}
            className={styles.form}
            onSubmit={submit}
            noValidate
            aria-label="Reset your PIN"
          >
            {Object.keys(errors).length > 0 && (
              <FieldErrorSummary
                errors={errors}
                fieldName={fieldName}
                headingRef={summaryRef}
                onSelect={focusField}
              />
            )}
            <div data-field="newPin">
              <PinField
                label="New PIN"
                name="newPin"
                autoComplete="new-password"
                value={newPin}
                onChange={setNewPin}
                errorMessage={errors.newPin}
              />
            </div>
            <div data-field="confirmPin">
              <PinField
                label="Confirm New PIN"
                name="confirmPin"
                autoComplete="new-password"
                value={confirmPin}
                onChange={setConfirmPin}
                errorMessage={errors.confirmPin}
              />
            </div>

            {generalError && (
              <InlineAlert
                variant="danger"
                role="alert"
                description={`Your PIN was not reset. ${generalError.message}`}
              />
            )}

            <div>
              <Button
                variant="primary"
                type="submit"
                isDisabled={reset.isPending}
              >
                Submit
              </Button>
            </div>
          </form>
        </>
      )}
    </div>
  );
}
