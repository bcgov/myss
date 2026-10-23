import { useEffect, useRef } from "react";

import styles from "./ErrorSummary.module.css";

export interface ErrorSummaryItem {
  message: string;
  /** Makes the item a button, for example one that focuses the field it is about. */
  onSelect?: () => void;
}

type ErrorSummaryProps = Readonly<{
  items: readonly ErrorSummaryItem[];
  /**
   * Focus moves to the heading on mount and whenever this value changes, if
   * there are items. Null never moves focus.
   */
  focusKey: string | number | null;
  /**
   * Gives the box `role="alert"`, so it is announced when it appears. Set it
   * for the component's lifetime: adding the role to a box already shown is
   * not announced.
   */
  announce: boolean;
}>;

/**
 * The "There is a problem" box: every reason at once, each optionally a
 * button. Renders nothing without items, so a caller can keep it mounted and
 * focus follows `focusKey` alone.
 */
export default function ErrorSummary({
  items,
  focusKey,
  announce,
}: ErrorSummaryProps) {
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    if (focusKey !== null) headingRef.current?.focus();
  }, [focusKey]);

  if (items.length === 0) return null;

  return (
    <div className={styles.errorSummary} role={announce ? "alert" : undefined}>
      <h2 ref={headingRef} tabIndex={-1} className={styles.errorTitle}>
        There is a problem
      </h2>
      <ul className={styles.errorList}>
        {items.map((item, index) => (
          <li key={`${item.message}:${index}`}>
            {item.onSelect ? (
              <button
                type="button"
                className={styles.errorLink}
                onClick={item.onSelect}
              >
                {item.message}
              </button>
            ) : (
              item.message
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
