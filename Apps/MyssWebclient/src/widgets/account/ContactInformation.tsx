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
  useUpdatePhones,
  type AccountPayload,
} from "@/hooks/useAccount";
import {
  formatPhone,
  PHONE_FORMAT_MESSAGE,
  PHONE_TYPES,
  phoneDigits,
  phoneTypeLabel,
  type PhoneType,
} from "@/lib/phone";
import styles from "./ContactInformation.module.css";

// Contact Information on Account Info (MYSS-271). Read-only until the citizen
// chooses Edit; then the phone numbers can be changed, added and removed.
// Email is shown but not editable (a separate story), and the mailing address
// comes from MIS, so it is read-only too.

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
      errors[`phones[${i}].number`] = PHONE_FORMAT_MESSAGE;
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

export default function ContactInformation({
  account,
}: Readonly<{ account: AccountPayload }>) {
  const save = useUpdatePhones();
  const [isEditing, setIsEditing] = useState(false);
  const [rows, setRows] = useState<PhoneRow[]>([]);
  const [nextKey, setNextKey] = useState(0);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [isSaved, setIsSaved] = useState(false);
  const formRef = useRef<HTMLFormElement>(null);
  const summaryRef = useRef<HTMLHeadingElement>(null);
  // BCDS Button does not forward a ref, so focus goes through its wrapper.
  const editActionRef = useRef<HTMLDivElement>(null);

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
    setErrors({});
    setIsSaved(false);
    save.reset();
    setIsEditing(true);
  }

  function stopEditing() {
    setIsEditing(false);
    // The edit button is back in place of the form; keep focus on it.
    requestAnimationFrame(() =>
      editActionRef.current?.querySelector("button")?.focus(),
    );
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

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const found = validate(rows);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    save.mutate(
      rows.map((row) => ({ number: row.number, type: row.type! })),
      {
        onSuccess: () => {
          setIsSaved(true);
          stopEditing();
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

  // A refusal with no field errors (401, 500, offline) has only its message.
  const generalError =
    save.error &&
    !(save.error instanceof AccountRequestError && save.error.errors.length > 0)
      ? save.error
      : null;

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

      <TextField label="Email Address" value={account.email} isReadOnly />

      {isEditing ? (
        <form
          ref={formRef}
          className={styles.form}
          onSubmit={submit}
          noValidate
          aria-label="Edit phone numbers"
        >
          {Object.keys(errors).length > 0 && (
            <ErrorSummary
              errors={errors}
              headingRef={summaryRef}
              onSelect={focusField}
            />
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
                  <TextField
                    label="Phone Number"
                    type="tel"
                    autoComplete="tel-national"
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

          <div className={styles.actions}>
            <Button variant="primary" type="submit" isDisabled={save.isPending}>
              Save
            </Button>
            <Button
              variant="secondary"
              onPress={stopEditing}
              isDisabled={save.isPending}
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
  const index = /^phones\[(\d+)\]/.exec(field)?.[1];
  return index === undefined
    ? "Phone numbers"
    : `Phone number ${Number(index) + 1}`;
}

/**
 * Every reason the save was refused, at the top of the form, each one a button
 * that moves focus to its field (Docs/accessibility.md, error summaries). The
 * same messages also sit on the fields themselves. Buttons, not links: they
 * move focus within the page rather than navigate.
 */
function ErrorSummary({
  errors,
  headingRef,
  onSelect,
}: Readonly<{
  errors: FieldErrors;
  headingRef: React.RefObject<HTMLHeadingElement | null>;
  onSelect: (field: string) => void;
}>) {
  return (
    <div className={styles.summary} role="alert">
      <h3 ref={headingRef} tabIndex={-1} className={styles.summaryHeading}>
        There is a problem
      </h3>
      <ul className={styles.summaryList}>
        {Object.entries(errors).map(([field, message]) => (
          <li key={field}>
            <button
              type="button"
              className={styles.summaryLink}
              onClick={() => onSelect(field)}
            >
              {fieldName(field)}: {message}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
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
        <TextField
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
