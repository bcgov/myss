import { useEffect, useRef } from "react";

import { SubmissionRejectedError } from "@/hooks/usePocForm";

function focusField(field: string) {
  const input = document.querySelector<HTMLElement>(
    `[name="data[${CSS.escape(field)}]"]`,
  );
  if (!input) return;

  input.focus();
  input.scrollIntoView({ block: "center", behavior: "smooth" });
}

export default function SubmissionErrors({ error }: { error: Error }) {
  const headingRef = useRef<HTMLHeadingElement>(null);
  const errors = error instanceof SubmissionRejectedError ? error.errors : [];

  useEffect(() => {
    headingRef.current?.focus();
  }, [error]);

  return (
    <div className="poc-form-errors" role="alert">
      <h4 ref={headingRef} tabIndex={-1}>
        There is a problem
      </h4>
      {errors.length === 0 ? (
        <p>{error.message}</p>
      ) : (
        <ul>
          {errors.map((validationError) => (
            <li key={`${validationError.field}:${validationError.keyword}`}>
              <button
                type="button"
                onClick={() => focusField(validationError.field)}
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
