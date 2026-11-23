import { useEffect, useRef, useState } from "react";

import {
  AttachmentUploadError,
  attachmentErrorMessage,
  uploadAttachment,
} from "@/api/attachments";
import {
  type AccountCreationAnswers,
  type AccountDocument,
  type DocumentField,
  documentFieldsFor,
} from "@/lib/accountContext";
import {
  deleteKeptFile,
  keepAnswers,
  takeKeptFile,
} from "@/lib/keptAccountCreation";

const GENERAL_REFUSAL = "The document could not be attached. Try again later.";
const NOT_KEPT = "This browser did not keep the file while you signed in.";

export interface RefusedDocument {
  field: DocumentField;
  fileName: string;
  message: string;
}

export interface KeptDocumentsUpload {
  state: "attaching" | "done";
  done: number;
  total: number;
  /** Each document field's files once uploaded, those attached before included. */
  attached: Partial<Record<DocumentField, AccountDocument[]>>;
  refused: RefusedDocument[];
}

interface Pending {
  field: DocumentField;
  document: AccountDocument;
}

function pendingFiles(answers: AccountCreationAnswers | null): Pending[] {
  if (!answers) return [];
  return documentFieldsFor(answers).flatMap((field) =>
    answers[field]
      .filter((document) => document.state === "kept")
      .map((document) => ({ field, document })),
  );
}

// The answers with the files uploaded so far, and those still to upload.
function answersSoFar(
  answers: AccountCreationAnswers,
  attached: Partial<Record<DocumentField, AccountDocument[]>>,
  remaining: Pending[],
): AccountCreationAnswers {
  const next = { ...answers };
  for (const field of documentFieldsFor(answers)) {
    next[field] = [
      ...(attached[field] ?? []),
      ...remaining.filter((p) => p.field === field).map((p) => p.document),
    ];
  }
  return next;
}

function refusalMessage(reason: unknown): string {
  return reason instanceof AttachmentUploadError
    ? (attachmentErrorMessage(reason.keyword) ?? GENERAL_REFUSAL)
    : GENERAL_REFUSAL;
}

/** Uploads one kept file, and deletes it from the tab whatever the API answered. */
async function attachOne(
  document: AccountDocument,
): Promise<AccountDocument | string> {
  try {
    const file = await takeKeptFile(document.id);
    if (!file) return NOT_KEPT;
    const stored = await uploadAttachment(file);
    return {
      id: stored.id,
      fileName: stored.fileName,
      sizeBytes: stored.sizeBytes,
      state: "attached",
    };
  } catch (reason) {
    return refusalMessage(reason);
  } finally {
    await deleteKeptFile(document.id);
  }
}

/**
 * Uploads the files the tab kept before sign-in, one at a time, as the
 * signed-in citizen, who then owns them, and keeps the tab's answers in step.
 * Runs once, however often the page renders, so no file is uploaded twice.
 */
export function useAttachKeptDocuments(
  answers: AccountCreationAnswers | null,
  onDone?: (upload: KeptDocumentsUpload) => void,
): KeptDocumentsUpload {
  const [upload, setUpload] = useState<KeptDocumentsUpload>(() => {
    const total = pendingFiles(answers).length;
    return {
      state: total > 0 ? "attaching" : "done",
      done: 0,
      total,
      attached: {},
      refused: [],
    };
  });
  const started = useRef(false);
  const onDoneRef = useRef(onDone);
  useEffect(() => {
    onDoneRef.current = onDone;
  });

  useEffect(() => {
    const pending = pendingFiles(answers);
    if (started.current || !answers || pending.length === 0) return;
    started.current = true;

    const attached: Partial<Record<DocumentField, AccountDocument[]>> = {};
    for (const field of documentFieldsFor(answers)) {
      attached[field] = answers[field].filter((d) => d.state === "attached");
    }
    const refused: RefusedDocument[] = [];

    void (async () => {
      for (const [index, { field, document }] of pending.entries()) {
        const outcome = await attachOne(document);
        if (typeof outcome === "string") {
          refused.push({
            field,
            fileName: document.fileName,
            message: outcome,
          });
        } else {
          attached[field]?.push(outcome);
        }
        // Kept after each file, so a reload looks for none already uploaded.
        keepAnswers(answersSoFar(answers, attached, pending.slice(index + 1)));
        setUpload((current) => ({ ...current, done: index + 1 }));
      }
      const finished: KeptDocumentsUpload = {
        state: "done",
        done: pending.length,
        total: pending.length,
        attached,
        refused,
      };
      setUpload(finished);
      onDoneRef.current?.(finished);
    })();
  }, [answers]);

  return upload;
}
