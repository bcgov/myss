import { useId } from "react";
import { Checkbox } from "@bcgov/design-system-react-components";

import styles from "./bcgovFields.module.css";

/**
 * Form.io's `checkbox`, drawn with the design system Checkbox. The label is
 * HTML the form authored, already sanitised by the caller, because a consent
 * checkbox links to the terms it refers to and Form.io's own checkbox rendered
 * it that way. The Checkbox has no message slot of its own, so the message is
 * rendered beside it and linked for screen readers.
 *
 * A checkbox can sit under a question of its own ("Acknowledgement", "Can we
 * leave a message?"): given `groupLabel`, that question is drawn above it as
 * a label, the checkbox and question are grouped so the question names the
 * checkbox for screen readers, and "(required)" moves from the checkbox's
 * text to the question, which is where a citizen looks for it.
 */
export default function CheckboxField({
  groupLabel,
  labelHtml,
  checked,
  isRequired,
  isDisabled,
  errorMessage,
  name,
  onChange,
}: Readonly<{
  /** A question above the checkbox, as plain text. */
  groupLabel?: string;
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
  const groupLabelId = useId();
  // Under a question the checkbox and the question form a group: a fieldset,
  // named by the question.
  const Wrapper = groupLabel ? "fieldset" : "div";
  return (
    <Wrapper
      className={styles.checkboxField}
      aria-labelledby={groupLabel ? groupLabelId : undefined}
    >
      {groupLabel && (
        <span
          id={groupLabelId}
          className="bcds-react-aria-CheckboxGroup--label"
        >
          {groupLabel}
          {isRequired && <span data-myss-required=""> (required)</span>}
        </span>
      )}
      <Checkbox
        isSelected={checked}
        // Under a question, the question carries "(required)"; the design
        // system would otherwise repeat it after the checkbox's own text.
        isRequired={isRequired && !groupLabel}
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
    </Wrapper>
  );
}
