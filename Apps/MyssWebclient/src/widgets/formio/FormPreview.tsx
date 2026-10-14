import { Button } from "@bcgov/design-system-react-components";
import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import "@formio/js/dist/formio.form.min.css";

import SubmissionErrors from "@/components/SubmissionErrors";
import { useClientValidation } from "@/hooks/useClientValidation";
import { registerBcgovComponents } from "./bcgovComponents";
import styles from "./FormPreview.module.css";

registerBcgovComponents();

interface FormSubmission {
  data: Record<string, unknown>;
}

/**
 * Renders a spec as citizens see it, with live conditions and validation.
 * Submitting shows the answers that would be sent; nothing is sent.
 */
export default function FormPreview({ spec }: Readonly<{ spec: FormType }>) {
  const clientValidation = useClientValidation();
  const { clear: clearClientValidation } = clientValidation;
  const [answers, setAnswers] = useState<Record<string, unknown> | null>(null);
  const formOptions = useMemo(() => ({ noAlerts: true }), []);

  // A different spec starts over: answers and blocked-submit messages from the
  // previous one no longer apply. An equal spec keeps what was entered.
  const specText = useMemo(() => JSON.stringify(spec), [spec]);
  const [shownSpecText, setShownSpecText] = useState(specText);
  if (shownSpecText !== specText) {
    setShownSpecText(specText);
    setAnswers(null);
    clearClientValidation();
  }

  // Submit and Start again remove the element that had focus, so focus moves
  // to what replaces it. A spec change never moves focus.
  const focusNext = useRef<"result" | "form" | null>(null);
  const resultHeadingRef = useRef<HTMLHeadingElement>(null);
  const formRegionRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const target = focusNext.current;
    focusNext.current = null;
    if (target === "result") resultHeadingRef.current?.focus();
    if (target === "form") formRegionRef.current?.focus();
  }, [answers]);

  const handleSubmit = useCallback(
    (submission: FormSubmission) => {
      clearClientValidation();
      focusNext.current = "result";
      setAnswers(submission.data);
    },
    [clearClientValidation],
  );

  const startAgain = useCallback(() => {
    focusNext.current = "form";
    setAnswers(null);
  }, []);

  if (answers) {
    return (
      <div className={styles.result}>
        <h3
          ref={resultHeadingRef}
          tabIndex={-1}
          className={styles.resultHeading}
        >
          The form would submit. These answers would be sent:
        </h3>
        <pre className={styles.answers}>{JSON.stringify(answers, null, 2)}</pre>
        <div>
          <Button variant="secondary" size="small" onPress={startAgain}>
            Start again
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div
      ref={formRegionRef}
      role="region"
      aria-label="Form preview"
      tabIndex={-1}
      className={styles.formRegion}
    >
      {clientValidation.error && (
        <SubmissionErrors error={clientValidation.error} />
      )}
      <Form
        src={spec}
        options={formOptions}
        onSubmitError={clientValidation.onSubmitError}
        onSubmit={handleSubmit}
      />
    </div>
  );
}
