import {
  Button,
  InlineAlert,
  Select,
  TextArea,
  TextField,
} from "@bcgov/design-system-react-components";
import { useEffect, useRef, useState } from "react";

import {
  AccountRequestError,
  useChangeEmail,
  useUpdatePhones,
  type AccountPayload,
  type AccountPhone,
} from "@/hooks/useAccount";
import { useFeature } from "@/hooks/useFeatures";
import {
  isEmailAddress,
  PhoneInput,
  RULE_DEFAULT_MESSAGES,
  RULE_KEYWORDS,
} from "@myss/formio";
import { catalogueMessage } from "@myss/formio/errorCatalogue";
import {
  formatPhone,
  PHONE_FORMAT_MESSAGE,
  PHONE_TYPES,
  phoneDigits,
  phoneTypeLabel,
  type PhoneType,
} from "@myss/formio/phone";
import styles from "./ContactInformation.module.css";
import EmailAddress from "./EmailAddress";
import FieldErrorSummary from "./FieldErrorSummary";
import { refusal } from "./refusal";

// Contact Information on Account Info (MYSS-271). Read-only until the citizen
// chooses Edit; then one form changes the email address (verified by link,
// MYSS-305, so it waits as pending; see EmailAddress) and the phone numbers,
// which can be changed, added and removed. The mailing address comes from
// MIS, so it is read-only.

const EMAIL_FORMAT_MESSAGE =
  catalogueMessage(RULE_KEYWORDS.emailInvalidFormat) ??
  RULE_DEFAULT_MESSAGES[RULE_KEYWORDS.emailInvalidFormat];

interface PhoneRow {
  /** Stable React key; rows can be removed from the middle. */
  key: number;
  number: string;
  type: PhoneType | null;
}

/** Field errors keyed the way the API reports them: `phones[0].number`. */
type FieldErrors = Record<string, string>;

const TYPE_ITEMS = PHONE_TYPES.map(({ type, label }) => ({ id: type, label }));

/** Mirrors the API's checks, so a mistake is caught before the round trip. */
function validate(rows: readonly PhoneRow[]): FieldErrors {
  const errors: FieldErrors = {};
  const seen = new Set<PhoneType>();
  rows.forEach((row, i) => {
    if (phoneDigits(row.number) === null) {
      errors[`phones[${i}].number`] =
        catalogueMessage("IDA.PHONE.INVALID_FORMAT") ?? PHONE_FORMAT_MESSAGE;
    }
    if (row.type === null) {
      errors[`phones[${i}].type`] =
        "Choose a phone type: home, cell, work or message.";
    } else if (seen.has(row.type)) {
      errors[`phones[${i}].type`] =
        "You can have one phone number of each type. Choose a different type.";
    } else {
      seen.add(row.type);
    }
  });
  return errors;
}

/** Only a changed list is sent, so an email-only edit does not rewrite the phones. */
function phonesChanged(
  rows: readonly PhoneRow[],
  phones: readonly AccountPhone[],
): boolean {
  return (
    rows.length !== phones.length ||
    rows.some(
      (row, i) =>
        phoneDigits(row.number) !== phones[i].number ||
        row.type !== phones[i].type,
    )
  );
}

export default function ContactInformation({
  account,
}: Readonly<{ account: AccountPayload }>) {
  const save = useUpdatePhones();
  const change = useChangeEmail();
  const canChangeEmail = useFeature("EmailVerification") === true;
  const [isEditing, setIsEditing] = useState(false);
  const [newEmail, setNewEmail] = useState("");
  const [rows, setRows] = useState<PhoneRow[]>([]);
  const [nextKey, setNextKey] = useState(0);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [isSaved, setIsSaved] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const summaryRef = useRef<HTMLHeadingElement>(null);
  // BCDS Button does not forward a ref, so focus goes through its wrapper.
  const editActionRef = useRef<HTMLDivElement>(null);
  // Set when the form opens or closes, so the effect below can move focus
  // into the form or back to the edit button. An effect runs after React has
  // put them on the page; an animation frame can run first under load and
  // leave focus on the body.
  const focusAfterToggle = useRef<"form" | "editAction" | null>(null);

  useEffect(() => {
    const target = focusAfterToggle.current;
    focusAfterToggle.current = null;
    if (target === "form") {
      formRef.current?.querySelector<HTMLElement>("input, button")?.focus();
    } else if (target === "editAction") {
      editActionRef.current?.querySelector("button")?.focus();
    }
  }, [isEditing]);

  // After a refused save, move focus to the error summary, so a screen-reader
  // user hears that the save failed and can go from there to each field.
  useEffect(() => {
    if (Object.keys(errors).length === 0) return;
    summaryRef.current?.focus();
  }, [errors]);

  /** Focuses the input (or Select trigger) an error key like `phones[0].type` belongs to. */
  function focusField(field: string) {
    formRef.current
      ?.querySelector(`[data-field="${CSS.escape(field)}"]`)
      ?.querySelector<HTMLElement>("input, button")
      ?.focus();
  }

  function startEditing() {
    setRows(
      account.phones.map((phone, i) => ({
        key: i,
        number: formatPhone(phone.number),
        type: phone.type,
      })),
    );
    setNextKey(account.phones.length);
    setNewEmail("");
    setErrors({});
    setIsSaved(false);
    save.reset();
    change.reset();
    focusAfterToggle.current = "form";
    setIsEditing(true);
  }

  function stopEditing() {
    // The edit button is back in place of the form; keep focus on it.
    focusAfterToggle.current = "editAction";
    setIsEditing(false);
  }

  function updateRow(key: number, change: Partial<PhoneRow>) {
    setRows((current) =>
      current.map((row) => (row.key === key ? { ...row, ...change } : row)),
    );
  }

  function addRow() {
    // Offer the first type not already used, so a new row starts valid.
    const used = new Set(rows.map((row) => row.type));
    const free = PHONE_TYPES.find(({ type }) => !used.has(type));
    setRows((current) => [
      ...current,
      { key: nextKey, number: "", type: free?.type ?? null },
    ]);
    setNextKey((key) => key + 1);
  }

  function removeRow(key: number) {
    setRows((current) => current.filter((row) => row.key !== key));
    setErrors({});
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const email = canChangeEmail ? newEmail.trim() : "";
    const found = validate(rows);
    if (email !== "" && !isEmailAddress(email)) {
      found.email = EMAIL_FORMAT_MESSAGE;
    }
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    const savePhones = phonesChanged(rows, account.phones);
    save.reset();
    change.reset();
    try {
      // Phones first: if the email change is then refused, the saved list
      // matches the account and a retry sends only the email.
      if (savePhones) {
        await save.mutateAsync(
          rows.map((row) => ({ number: row.number, type: row.type! })),
        );
      }
      if (email !== "") {
        await change.mutateAsync(email);
      }
    } catch (error) {
      if (error instanceof AccountRequestError && error.errors.length > 0) {
        setErrors(
          Object.fromEntries(
            error.errors.map((fieldError) => [
              fieldError.field,
              fieldError.keyword === "IDA.PHONE.INVALID_FORMAT"
                ? (catalogueMessage(fieldError.keyword) ?? fieldError.message)
                : fieldError.message,
            ]),
          ),
        );
      }
      return;
    }
    setIsSaved(savePhones || email !== "");
    stopEditing();
  }

  // A refusal with no field errors (401, 500, offline) has only its message.
  const generalError =
    save.error &&
    !(save.error instanceof AccountRequestError && save.error.errors.length > 0)
      ? save.error
      : null;
  const emailError =
    change.error &&
    !(
      change.error instanceof AccountRequestError &&
      change.error.errors.length > 0
    )
      ? refusal(change.error, "Your email address was not changed.")
      : null;
  const isPending = save.isPending || change.isPending;

  return (
    <section aria-labelledby="contact-information" className={styles.section}>
      <h2 id="contact-information">Contact Information</h2>

      {isSaved && (
        <output className={styles.confirmation}>
          <InlineAlert
            variant="success"
            description="Your contact information has been updated."
          />
        </output>
      )}

      <EmailAddress account={account} isEditing={isEditing} />

      {isEditing ? (
        <form
          ref={formRef}
          className={styles.form}
          onSubmit={submit}
          noValidate
          aria-label="Edit contact information"
        >
          {Object.keys(errors).length > 0 && (
            <FieldErrorSummary
              errors={errors}
              fieldName={fieldName}
              headingRef={summaryRef}
              onSelect={focusField}
            />
          )}
          {canChangeEmail && (
            <div className={styles.email} data-field="email">
              <TextField
                label="New Email Address"
                name="email"
                type="email"
                autoComplete="email"
                value={newEmail}
                onChange={setNewEmail}
                isInvalid={Boolean(errors.email)}
                errorMessage={errors.email}
                description="Leave blank to keep your current address. We will email a link to a new address; your email address changes when you follow it."
              />
            </div>
          )}
          {rows.length === 0 && <p>No phone numbers. Add one below.</p>}
          {rows.map((row, i) => (
            <fieldset key={row.key} className={styles.phoneEdit}>
              <legend className={styles.visuallyHidden}>
                Phone number {i + 1}
              </legend>
              {/* Layout classes go on wrappers: a className on a BCDS field
                  replaces its own and drops the design system's styling. */}
              <div className={styles.phoneRow}>
                <div
                  className={styles.number}
                  data-field={`phones[${i}].number`}
                >
                  <PhoneInput
                    label="Phone Number"
                    value={row.number}
                    onChange={(number) => updateRow(row.key, { number })}
                    isInvalid={Boolean(errors[`phones[${i}].number`])}
                    errorMessage={errors[`phones[${i}].number`]}
                  />
                </div>
                <div className={styles.type} data-field={`phones[${i}].type`}>
                  <Select
                    label="Type"
                    items={TYPE_ITEMS}
                    value={row.type}
                    onChange={(type) =>
                      updateRow(row.key, { type: type as PhoneType | null })
                    }
                    isInvalid={Boolean(errors[`phones[${i}].type`])}
                    errorMessage={errors[`phones[${i}].type`]}
                  />
                </div>
              </div>
              <div>
                <Button
                  variant="tertiary"
                  size="small"
                  aria-label={`Remove phone number ${i + 1}`}
                  onPress={() => removeRow(row.key)}
                >
                  Remove
                </Button>
              </div>
            </fieldset>
          ))}

          {/* One number per type, so the list is full at four. */}
          {rows.length < PHONE_TYPES.length && (
            <div>
              <Button variant="tertiary" size="small" onPress={addRow}>
                Add phone number
              </Button>
            </div>
          )}

          {generalError && (
            <InlineAlert
              variant="danger"
              role="alert"
              description={`Your changes were not saved. ${generalError.message}`}
            />
          )}
          {emailError && (
            <InlineAlert
              variant={emailError.variant}
              role="alert"
              description={emailError.text}
            />
          )}

          <div className={styles.actions}>
            <Button variant="primary" type="submit" isDisabled={isPending}>
              Save
            </Button>
            <Button
              variant="secondary"
              onPress={stopEditing}
              isDisabled={isPending}
            >
              Cancel
            </Button>
          </div>
        </form>
      ) : (
        <PhoneList account={account} />
      )}

      <TextArea
        label="Mailing Address"
        value={account.mailingAddressLines.join("\n")}
        isReadOnly
      />

      {!isEditing && (
        <div ref={editActionRef}>
          <Button variant="secondary" onPress={startEditing}>
            Edit contact information
          </Button>
        </div>
      )}
    </section>
  );
}

/** "phones[1].type" reads as "Phone number 2"; anything else as a whole. */
function fieldName(field: string): string {
  if (field === "email") return "New Email Address";
  const index = /^phones\[(\d+)\]/.exec(field)?.[1];
  return index === undefined
    ? "Phone numbers"
    : `Phone number ${Number(index) + 1}`;
}

function PhoneList({ account }: Readonly<{ account: AccountPayload }>) {
  if (account.phones.length === 0) {
    return <p>No phone numbers added.</p>;
  }
  return account.phones.map((phone, i) => (
    <div
      key={phone.type}
      role="group"
      aria-label={`Phone number ${i + 1}`}
      className={styles.phoneRow}
    >
      <div className={styles.number}>
        <PhoneInput
          label="Phone Number"
          value={formatPhone(phone.number)}
          isReadOnly
        />
      </div>
      <div className={styles.type}>
        <TextField label="Type" value={phoneTypeLabel(phone.type)} isReadOnly />
      </div>
    </div>
  ));
}
