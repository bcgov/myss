import { useCallback, useState } from "react";

import { SubmissionRejectedError } from "@/api/forms";
import { toValidationErrors } from "@/lib/formioErrors";

// A submit Form.io blocked, in the shape a submit the API refused arrives in,
// so both feed the same accessible error summary (SubmissionErrors). Wire
// `onSubmitError` to the <Form>, show `error` beside the API's, and `clear` it
// when a submit gets through (Form.io's onSubmit only fires for a valid one).

export function useClientValidation() {
  const [error, setError] = useState<SubmissionRejectedError | null>(null);

  const onSubmitError = useCallback((raw: unknown) => {
    const errors = toValidationErrors(raw);
    setError(errors.length > 0 ? new SubmissionRejectedError(0, errors) : null);
  }, []);

  const clear = useCallback(() => setError(null), []);

  return { error, onSubmitError, clear };
}
