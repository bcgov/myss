import { useEffect, useRef } from "react";

import styles from "./SubmissionErrors.module.css";
import { SubmissionRejectedError } from "@/api/forms";

/**
 * Moves focus to the input a validation error belongs to.
 *
 * Form.io names its inputs `data[<key>]`, and the API reports failures by
 * component key, so the two line up without the client needing to know
 * anything about Form.io's generated element ids (which are random per render
 * and therefore useless as anchor targets).
 *
 * A miss is deliberately silent: the message is already on screen, and a field
 * that cannot be focused, hidden by a conditional say, is not worth throwing
 * over.
 */
function focusField(field: string) {
    const input = document.querySelector<HTMLElement>(
        `[name="data[${CSS.escape(field)}]"]`,
    );
    if (!input) return;

    input.focus();
    input.scrollIntoView({ block: "center", behavior: "smooth" });
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
export default function SubmissionErrors({ error }: { error: Error }) {
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
