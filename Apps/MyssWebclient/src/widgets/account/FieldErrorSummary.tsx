import styles from "./FieldErrorSummary.module.css";

/**
 * Every reason a save on Account Info was refused, at the top of its form,
 * each one a button that moves focus to its field (Docs/accessibility.md,
 * error summaries). The same messages also sit on the fields themselves.
 * Buttons, not links: they move focus within the page rather than navigate.
 * Shared by Contact Information (MYSS-271) and PIN management (MYSS-258).
 */
export default function FieldErrorSummary({
  errors,
  fieldName,
  headingRef,
  onSelect,
}: Readonly<{
  /** Messages keyed by the field the API reports them on. */
  errors: Readonly<Record<string, string>>;
  /** How a field key reads to the citizen, e.g. "Phone number 2". */
  fieldName: (field: string) => string;
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
