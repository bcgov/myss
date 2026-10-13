import { Button, InlineAlert } from "@bcgov/design-system-react-components";
import { useEffect, useRef, useState } from "react";

import PinField from "@/components/PinField";
import {
  AccountRequestError,
  PIN_LOCKED_KEYWORD,
  useSavePin,
  type AccountPayload,
} from "@/hooks/useAccount";
import { checkNewPin, checkPin, pinMessage } from "@/lib/pin";
import sectionStyles from "./AccountSections.module.css";
import FieldErrorSummary from "./FieldErrorSummary";
import styles from "./PinManagement.module.css";

// PIN management on Account Info. Change PIN (MYSS-258) opens an inline
// section asking for the current PIN and the new one twice. A Basic BCeID
// citizen who registered before PINs were asked for creates one here
// instead. BC Services Card users have no PIN, so the section is not shown.
// Reset PIN (MYSS-36) is its own story; its button is still a placeholder.

/** Field errors keyed the way the API reports them: `currentPin` and so on. */
type FieldErrors = Record<string, string>;

const FIELD_NAMES: Readonly<Record<string, string>> = {
  currentPin: "Current PIN",
  newPin: "New PIN",
  confirmPin: "Confirm New PIN",
};

function fieldName(field: string): string {
  return FIELD_NAMES[field] ?? "PIN";
}

/** Mirrors the API's checks, so a mistake is caught before the round trip. */
function validate(
  isChange: boolean,
  current: string,
  next: string,
  confirm: string,
): FieldErrors {
  const errors: FieldErrors = {};
  const currentKeyword = isChange ? checkPin(current) : null;
  if (currentKeyword) errors.currentPin = pinMessage(currentKeyword);
  for (const error of checkNewPin(next, confirm, {
    pin: "newPin",
    confirmation: "confirmPin",
  })) {
    errors[error.field] = error.message;
  }
  return errors;
}

export default function PinManagement({
  account,
}: Readonly<{ account: AccountPayload }>) {
  const save = useSavePin();
  const [isOpen, setIsOpen] = useState(false);
  const [currentPin, setCurrentPin] = useState("");
  const [newPin, setNewPin] = useState("");
  const [confirmPin, setConfirmPin] = useState("");
  const [errors, setErrors] = useState<FieldErrors>({});
  const [savedMessage, setSavedMessage] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);
  const summaryRef = useRef<HTMLHeadingElement>(null);
  // BCDS Button does not forward a ref, so focus goes through its wrapper.
  const openActionRef = useRef<HTMLDivElement>(null);

  // After a refused save, move focus to the error summary, so a screen-reader
  // user hears that the save failed and can go from there to each field.
  useEffect(() => {
    if (Object.keys(errors).length === 0) return;
    summaryRef.current?.focus();
  }, [errors]);

  if (account.pinStatus === "NotApplicable") return null;

  const isChange = account.pinStatus === "Set";

  function focusField(field: string) {
    formRef.current
      ?.querySelector(`[data-field="${CSS.escape(field)}"]`)
      ?.querySelector<HTMLElement>("input")
      ?.focus();
  }

  function open() {
    setCurrentPin("");
    setNewPin("");
    setConfirmPin("");
    setErrors({});
    setSavedMessage(null);
    save.reset();
    setIsOpen(true);
    requestAnimationFrame(() => formRef.current?.querySelector("input")?.focus());
  }

  function close() {
    setIsOpen(false);
    // The open button is back in place of the form; keep focus on it.
    requestAnimationFrame(() =>
      openActionRef.current?.querySelector("button")?.focus(),
    );
  }

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const found = validate(isChange, currentPin, newPin, confirmPin);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    save.mutate(
      {
        ...(isChange ? { currentPin } : {}),
        newPin,
        confirmPin,
      },
      {
        onSuccess: () => {
          setSavedMessage(
            isChange ? "Your PIN has been changed." : "Your PIN has been created.",
          );
          close();
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
            // A wrong current PIN is typed again, not corrected.
            if (error.errors.some((e) => e.field === "currentPin")) {
              setCurrentPin("");
            }
          }
        },
      },
    );
  }

  // A refusal with no field errors (429 while locked, 401, 500, offline) has
  // only its message.
  const generalError =
    save.error &&
    !(save.error instanceof AccountRequestError && save.error.errors.length > 0)
      ? save.error
      : null;
  const isLocked =
    generalError instanceof AccountRequestError &&
    generalError.keyword === PIN_LOCKED_KEYWORD;

  return (
    <section aria-labelledby="pin-management" className={sectionStyles.section}>
      <h2 id="pin-management">PIN management</h2>
      <p>
        Choose a 4-digit number that will verify your identity when you sign and
        submit documents online.
      </p>

      {savedMessage && (
        <output className={styles.confirmation}>
          <InlineAlert variant="success" description={savedMessage} />
        </output>
      )}

      {isOpen ? (
        <form
          ref={formRef}
          className={styles.form}
          onSubmit={submit}
          noValidate
          aria-label={isChange ? "Change your PIN" : "Create your PIN"}
        >
          {Object.keys(errors).length > 0 && (
            <FieldErrorSummary
              errors={errors}
              fieldName={fieldName}
              headingRef={summaryRef}
              onSelect={focusField}
            />
          )}
          {isChange && (
            <div data-field="currentPin">
              <PinField
                label="Current PIN"
                name="currentPin"
                autoComplete="current-password"
                value={currentPin}
                onChange={setCurrentPin}
                errorMessage={errors.currentPin}
              />
            </div>
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
              description={
                isLocked
                  ? generalError.message
                  : `Your PIN was not saved. ${generalError.message}`
              }
            />
          )}

          <div className={styles.actions}>
            <Button
              variant="primary"
              type="submit"
              isDisabled={save.isPending || isLocked}
            >
              Save
            </Button>
            <Button
              variant="secondary"
              onPress={close}
              isDisabled={save.isPending}
            >
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        <div ref={openActionRef}>
          <Button variant="secondary" onPress={open}>
            {isChange ? "Change my PIN" : "Create a PIN"}
          </Button>
        </div>
      )}

      {isChange && (
        <>
          <p>We can email you a link to reset your 4-digit PIN.</p>
          <div>
            <Button variant="secondary">Reset PIN</Button>
          </div>
        </>
      )}
    </section>
  );
}
