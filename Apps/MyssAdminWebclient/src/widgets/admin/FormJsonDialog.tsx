import {
  Button,
  Dialog,
  Heading,
  Modal,
} from "@bcgov/design-system-react-components";
import { useId, useRef, useState } from "react";

import { FormLoadError } from "@/api/forms";
import { useFormVersion } from "@/hooks/useForms";
import styles from "./FormJsonDialog.module.css";

type FormJsonDialogProps = Readonly<{
  formSpecId: string;
  formName: string;
  version: number;
  isPublished: boolean;
  isOpen: boolean;
  onClose: () => void;
}>;

/**
 * One version of a form as read-only JSON, with Copy and Cancel. Escape and
 * Cancel close it, and focus returns to the control that opened it.
 */
export default function FormJsonDialog({
  formSpecId,
  formName,
  version,
  isPublished,
  isOpen,
  onClose,
}: FormJsonDialogProps) {
  return (
    <Modal
      isOpen={isOpen}
      isDismissable
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <Dialog isCloseable={false}>
        <div className={styles.content}>
          <Heading slot="title" level={2} className={styles.title}>
            {formName} · version {version} ·{" "}
            {isPublished ? "Published" : "Draft"}
          </Heading>
          <p className={styles.formId}>
            ID <code>{formSpecId}</code>
          </p>
          <VersionJson
            formSpecId={formSpecId}
            version={version}
            onClose={onClose}
          />
        </div>
      </Dialog>
    </Modal>
  );
}

// Mounted only while the dialog is open, and the version read keeps nothing
// between mounts, so each opening reads the version again.
function VersionJson({
  formSpecId,
  version,
  onClose,
}: Readonly<{ formSpecId: string; version: number; onClose: () => void }>) {
  const { data, error } = useFormVersion(formSpecId, version);
  const textId = useId();
  const textRef = useRef<HTMLTextAreaElement>(null);
  const [copyStatus, setCopyStatus] = useState("");
  const text = data ? JSON.stringify(data.spec, null, 2) : "";

  // The Clipboard API needs a secure context and the page's permission; when
  // it is refused, the JSON is selected so it can be copied by hand.
  async function copy() {
    // Cleared first, so a second "Copied." is announced again.
    setCopyStatus("");
    try {
      await navigator.clipboard.writeText(text);
      setCopyStatus("Copied.");
    } catch {
      textRef.current?.focus();
      textRef.current?.select();
      setCopyStatus(
        "Couldn't copy automatically. The JSON is selected; press Ctrl+C (Cmd+C on a Mac) to copy it.",
      );
    }
  }

  let body;
  if (data) {
    body = (
      <>
        <label htmlFor={textId} className={styles.label}>
          Form JSON
        </label>
        <textarea
          id={textId}
          ref={textRef}
          className={styles.json}
          value={text}
          readOnly
          rows={16}
          spellCheck={false}
        />
      </>
    );
  } else if (error) {
    const status =
      error instanceof FormLoadError ? ` (error ${error.status})` : "";
    body = (
      <p role="alert" className={styles.error}>
        Couldn't load version {version}
        {status}.
      </p>
    );
  } else {
    body = <p className={styles.loading}>Loading version {version}…</p>;
  }

  return (
    <>
      {body}
      <div className={styles.actions}>
        {data && <Button onPress={() => void copy()}>Copy</Button>}
        <Button variant="secondary" onPress={onClose}>
          Cancel
        </Button>
        <p role="status" className={styles.copyStatus}>
          {copyStatus}
        </p>
      </div>
    </>
  );
}
