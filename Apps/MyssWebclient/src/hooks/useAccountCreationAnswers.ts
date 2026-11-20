import { useCallback, useState } from "react";

import type { AttachmentPayload } from "@/api/attachments";
import { type FormValidationError, SubmissionRejectedError } from "@/api/forms";
import {
  ACCOUNT_CREATION_FIELDS,
  type AccountCreationAnswers,
  type AccountDocument,
  checkAccountCreation,
  type DocumentField,
  isAccountCreationField,
  withoutHiddenDocuments,
} from "@/lib/accountContext";
import {
  deleteKeptFile,
  keepAnswers,
  keepFile,
} from "@/lib/keptAccountCreation";

type Errors = Readonly<Record<string, string>>;

function fieldOf(key: string): string {
  return key === "context" ? ACCOUNT_CREATION_FIELDS.context : key;
}

function without(errors: Errors, fields: string[]): Errors {
  const next = { ...errors };
  for (const field of fields) delete next[field];
  return next;
}

// One message per field: the first, as the API reports them.
function byField(errors: readonly FormValidationError[]): Errors {
  const result: Record<string, string> = {};
  for (const error of errors) result[error.field] ??= error.message;
  return result;
}

/**
 * The account-creation answers and their errors, shared by the questions
 * before sign-in and after it. A changed field loses its error; the summary
 * keeps the last check's errors until the next one.
 */
export function useAccountCreationAnswers(
  initial: () => AccountCreationAnswers,
) {
  const [answers, setAnswers] = useState(initial);
  const [errors, setErrors] = useState<Errors>({});
  const [summary, setSummary] = useState<SubmissionRejectedError | null>(null);

  const change = useCallback((change: Partial<AccountCreationAnswers>) => {
    setAnswers((current) => ({ ...current, ...change }));
    setErrors((current) => without(current, Object.keys(change).map(fieldOf)));
  }, []);

  const addDocument = useCallback(
    (field: DocumentField, document: AccountDocument) => {
      setAnswers((current) => ({
        ...current,
        [field]: [...current[field], document],
      }));
      setErrors((current) => without(current, [field]));
    },
    [],
  );

  function showErrors(found: readonly FormValidationError[]) {
    setErrors(byField(found));
    setSummary(
      found.length > 0 ? new SubmissionRejectedError(422, found) : null,
    );
  }

  return {
    answers,
    errors,
    /** The last check's errors, for the page's error summary. */
    summary,
    change,
    /** Runs the browser's checks; true when the answers pass. */
    check(options: { canAttach: boolean }): boolean {
      const found = checkAccountCreation(answers, options);
      showErrors(found);
      return found.length === 0;
    },
    /**
     * Keeps the answers in the tab through the sign-in, without the
     * documents of questions the context no longer asks, which are deleted.
     */
    keep(options?: { filesNotKept?: boolean }) {
      const { answers: shown, hidden } = withoutHiddenDocuments(answers);
      for (const document of hidden) {
        if (document.state === "kept") void deleteKeptFile(document.id);
      }
      if (hidden.length > 0) setAnswers(shown);
      keepAnswers(shown, options);
    },
    /** Keeps a file in the tab and lists it; rejects when the tab cannot keep it. */
    async addFile(field: DocumentField, file: File): Promise<void> {
      const id = await keepFile(file);
      if (id === null)
        throw new Error("The file could not be kept in this tab.");
      addDocument(field, {
        id,
        fileName: file.name,
        sizeBytes: file.size,
        state: "kept",
      });
    },
    /** Lists a file the API has stored. */
    addUploaded(field: DocumentField, attachment: AttachmentPayload) {
      addDocument(field, {
        id: attachment.id,
        fileName: attachment.fileName,
        sizeBytes: attachment.sizeBytes,
        state: "attached",
      });
    },
    /** Takes a file off the list; a file kept in the tab is deleted from it. */
    removeFile(field: DocumentField, document: AccountDocument) {
      setAnswers((current) => ({
        ...current,
        [field]: current[field].filter((d) => d.id !== document.id),
      }));
      if (document.state === "kept") void deleteKeptFile(document.id);
    },
    /** Shows the API's refusals on the account-creation fields. */
    showServerErrors(found: readonly FormValidationError[]) {
      showErrors(found.filter((error) => isAccountCreationField(error.field)));
    },
  };
}
