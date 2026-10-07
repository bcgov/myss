import { useEffect, useRef } from "react";

import styles from "./SubmissionErrors.module.css";
import { SubmissionRejectedError } from "@/api/forms";

/** Whether an element can take focus the way a citizen would give it. */
function isFocusable(element: HTMLElement): boolean {
    return (
        element.tabIndex >= 0 &&
        !element.hidden &&
        element.getAttribute("type") !== "hidden" &&
        element.getAttribute("aria-hidden") !== "true"
    );
}

/**
 * Moves focus to the input a validation error belongs to.
 *
 * Every field's input is named `data[<key>]`, and the API reports failures by
 * component key, so the two line up without the client needing to know
 * anything about Form.io's generated element ids (which are random per render
 * and therefore useless as anchor targets). A design system select keeps that
 * name on a hidden element and takes focus on its button, so when the named
 * element cannot be focused the field's wrapper is searched for what can.
 *
 * A miss is deliberately silent: the message is already on screen, and a field
 * that cannot be focused, hidden by a conditional say, is not worth throwing
 * over.
 */
function focusField(field: string) {
    const key = CSS.escape(field);
    const named = document.querySelector<HTMLElement>(`[name="data[${key}]"]`);
    const target =
        named && isFocusable(named)
            ? named
            : Array.from(
                  document.querySelectorAll<HTMLElement>(
                      `.formio-component-${key} :is(input, select, textarea, button)`,
                  ),
              ).find(isFocusable);
    if (!target) return;

    target.focus();
    target.scrollIntoView({ block: "center", behavior: "smooth" });
}

/**
 * The error summary for a refused submission, shared by every Form.io-backed
 * form that posts to a MyssApi endpoint answering 422 in the forms shape.
 *
 * Rendered as a list of every reason at once rather than one at a time,
 * because that is the shape the 422 body is deliberately built in. Focus moves
 * to the heading when it appears, so a screen-reader user is told the
 * submission failed instead of being left at the submit button in silence.
 */
export default function SubmissionErrors({ error }: Readonly<{ error: Error }>) {
    const headingRef = useRef<HTMLHeadingElement>(null);
    const errors = error instanceof SubmissionRejectedError ? error.errors : [];

    useEffect(() => {
        headingRef.current?.focus();
    }, [error]);

    return (
        <div className={styles.summary} role="alert">
            <h4 ref={headingRef} tabIndex={-1} className={styles.heading}>
                There is a problem
            </h4>
            {errors.length === 0 ? (
                // Non-422 failures (401, 500, an HTML error page from a proxy) carry no
                // error collection; the thrown message is all there is to show.
                <p>{error.message}</p>
            ) : (
                <ul className={styles.list}>
                    {errors.map((validationError) => (
                        <li
                            key={`${validationError.field}:${validationError.keyword}`}
                        >
                            <button
                                type="button"
                                className={styles.fieldButton}
                                onClick={() =>
                                    focusField(validationError.field)
                                }
                            >
                                {validationError.message}
                            </button>
                        </li>
                    ))}
                </ul>
            )}
        </div>
    );
}
