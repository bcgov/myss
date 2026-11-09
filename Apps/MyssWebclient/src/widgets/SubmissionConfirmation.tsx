import { InlineAlert } from "@bcgov/design-system-react-components";
import type { FormMessageTexts } from "@myss/web-common/api";
import {
  MessageText,
  messageShowsPlaceholder,
} from "@myss/web-common/components";

import { useFocusOnMount } from "@/hooks/useFocusOnMount";
import styles from "./SubmissionConfirmation.module.css";

const REFERENCE_PLACEHOLDER = "referenceNumber";

/**
 * The page after an accepted request: the message's heading, focused, then a
 * success alert with the reference number, or else the submission ID, in place.
 */
export default function SubmissionConfirmation({
  message,
  referenceNumber,
  submissionId,
}: Readonly<{
  message: Readonly<FormMessageTexts>;
  referenceNumber?: string | null;
  submissionId: string;
}>) {
  const headingRef = useFocusOnMount();
  const reference = referenceNumber || submissionId;

  return (
    <section>
      <h1 ref={headingRef} tabIndex={-1} className={styles.heading}>
        {message.heading}
      </h1>
      <InlineAlert variant="success">
        {/* With children, the alert does not render its title prop, so the
            title uses the alert's own markup, which its aria-labelledby names. */}
        <span className="title" id="alert-title">
          {message.title}
        </span>
        <div className={styles.message}>
          <MessageText
            text={message.body}
            values={{ [REFERENCE_PLACEHOLDER]: reference }}
          />
          {!messageShowsPlaceholder(message.body, REFERENCE_PLACEHOLDER) && (
            <p>Your reference number is {reference}.</p>
          )}
        </div>
      </InlineAlert>
    </section>
  );
}
