import { useId } from "react";
import { Select, TextField } from "@bcgov/design-system-react-components";

import styles from "./bcgovFields.module.css";
import type { DateEntry, DateEntryPart } from "./validationRules";

/** One message under the date, and the parts it is about (drawn red). */
export interface DateInputError {
  readonly message: string;
  readonly parts: readonly DateEntryPart[];
}

const MONTHS = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
].map((label, index) => ({ id: String(index + 1), label }));

/**
 * A date as three answers side by side: a design system Select for the month
 * and TextFields for the day and the year, under one label. Each part turns
 * red on its own and the messages sit together under the row, each linked to
 * the inputs it is about. The label is drawn like a checkbox's question
 * (CheckboxField), so the theme styles it as it does every field label.
 */
export default function DateInputField({
  label,
  "aria-label": ariaLabel,
  description,
  isRequired,
  isDisabled,
  name,
  entry,
  isoValue,
  errors,
  onChange,
}: Readonly<{
  label?: string;
  "aria-label"?: string;
  description?: string;
  isRequired: boolean;
  isDisabled: boolean;
  /** Carried by a hidden input holding the ISO date, as every field's input is. */
  name: string;
  entry: DateEntry;
  isoValue: string;
  errors: readonly DateInputError[];
  onChange: (part: DateEntryPart, text: string) => void;
}>) {
  const id = useId();
  const labelId = `${id}-label`;
  const descriptionId = `${id}-description`;
  const errorId = (index: number) => `${id}-error-${index}`;

  const isInvalid = (part: DateEntryPart) =>
    errors.some((error) => error.parts.includes(part));
  const describedBy = (part: DateEntryPart) =>
    errors
      .flatMap((error, index) =>
        error.parts.includes(part) ? [errorId(index)] : [],
      )
      .join(" ") || undefined;

  return (
    <fieldset
      className={styles.dateInput}
      aria-labelledby={label ? labelId : undefined}
      aria-label={label ? undefined : ariaLabel}
      aria-describedby={description ? descriptionId : undefined}
    >
      {label && (
        <span id={labelId} className="bcds-react-aria-CheckboxGroup--label">
          {label}
          {isRequired && <span data-myss-required=""> (required)</span>}
        </span>
      )}
      <div className={styles.dateParts}>
        <div className={styles.dateMonth}>
          <Select
            label="Month"
            placeholder="Select a month"
            items={MONTHS}
            selectedKey={entry.month === "" ? null : entry.month}
            isDisabled={isDisabled}
            isInvalid={isInvalid("month")}
            validationBehavior="aria"
            aria-describedby={describedBy("month")}
            onSelectionChange={(key) => {
              onChange("month", key === null ? "" : String(key));
            }}
          />
        </div>
        <div className={styles.dateDay}>
          <TextField
            label="Day"
            value={entry.day}
            inputMode="numeric"
            maxLength={2}
            isDisabled={isDisabled}
            isInvalid={isInvalid("day")}
            validationBehavior="aria"
            aria-describedby={describedBy("day")}
            onChange={(text: string) => onChange("day", text)}
          />
        </div>
        <div className={styles.dateYear}>
          <TextField
            label="Year"
            value={entry.year}
            inputMode="numeric"
            maxLength={4}
            isDisabled={isDisabled}
            isInvalid={isInvalid("year")}
            validationBehavior="aria"
            aria-describedby={describedBy("year")}
            onChange={(text: string) => onChange("year", text)}
          />
        </div>
      </div>
      <input type="hidden" name={name} value={isoValue} />
      {description && (
        <div id={descriptionId} className={styles.dateDescription}>
          {description}
        </div>
      )}
      {errors.length > 0 && (
        <div className={styles.dateErrors}>
          {errors.map((error, index) => (
            <p
              key={error.message}
              id={errorId(index)}
              className={styles.fieldError}
            >
              {error.message}
            </p>
          ))}
        </div>
      )}
    </fieldset>
  );
}
