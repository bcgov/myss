import { useId } from "react";
import { Checkbox } from "@bcgov/design-system-react-components";

import styles from "./bcgovFields.module.css";

/**
 * Form.io's `checkbox`, drawn with the design system Checkbox. The label is
 * HTML the form authored, already sanitised by the caller, because a consent
 * checkbox links to the terms it refers to and Form.io's own checkbox rendered
 * it that way. The Checkbox has no message slot of its own, so the message is
 * rendered beside it and linked for screen readers.
 */
export default function CheckboxField({
  labelHtml,
  checked,
  isRequired,
  isDisabled,
  errorMessage,
  name,
  onChange,
}: Readonly<{
  /** The label as sanitised HTML; a link inside it stays a link. */
  labelHtml: string;
  checked: boolean;
  isRequired: boolean;
  isDisabled: boolean;
  errorMessage?: string;
  name: string;
  onChange: (next: boolean) => void;
}>) {
  const errorId = useId();
  return (
    <div className={styles.checkboxField}>
      <Checkbox
        isSelected={checked}
        isRequired={isRequired}
        isDisabled={isDisabled}
        isInvalid={errorMessage !== undefined}
        name={name}
        aria-describedby={errorMessage ? errorId : undefined}
        onChange={onChange}
      >
        <span dangerouslySetInnerHTML={{ __html: labelHtml }} />
      </Checkbox>
      {errorMessage && (
        <p id={errorId} className={styles.fieldError}>
          {errorMessage}
        </p>
      )}
    </div>
  );
}
