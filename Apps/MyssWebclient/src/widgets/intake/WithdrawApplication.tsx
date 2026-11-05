import { AlertDialog, Button, Modal } from "@bcgov/design-system-react-components";
import { useEffect, useId, useRef, useState } from "react";

import {
  isWithdrawableStatus,
  useWithdrawApplication,
  type ApplicationSummaryPayload,
} from "@/hooks/useIntake";

export default function WithdrawApplication({
  application,
}: Readonly<{ application: ApplicationSummaryPayload }>) {
  const [isOpen, setIsOpen] = useState(false);
  const messageId = useId();
  const outcomeRef = useRef<HTMLOutputElement>(null);
  const withdraw = useWithdrawApplication(
    application.id,
    application.formSpecId === "application-for-assistance",
  );
  useEffect(() => {
    if (withdraw.isSuccess && !isOpen) outcomeRef.current?.focus();
  }, [withdraw.isSuccess, isOpen]);

  return (
    <>
      {isWithdrawableStatus(application.status) && (
        <Button
          variant="secondary"
          aria-label={`Withdraw application ${application.referenceNumber}`}
          onPress={() => {
            withdraw.reset();
            setIsOpen(true);
          }}
        >
          Withdraw
        </Button>
      )}
      {withdraw.isSuccess && (
        <output ref={outcomeRef} tabIndex={-1}>Application withdrawn.</output>
      )}
      <Modal
        isOpen={isOpen}
        onOpenChange={(open) => {
          if (!withdraw.isPending) setIsOpen(open);
        }}
      >
        <AlertDialog
          variant="warning"
          role="alertdialog"
          title="Withdraw application"
          aria-label="Withdraw application"
          aria-describedby={messageId}
          isCloseable={false}
          buttons={
            <>
              <Button
                isDisabled={withdraw.isPending}
                onPress={() =>
                  withdraw.mutate(undefined, {
                    onSuccess: () => setIsOpen(false),
                  })
                }
              >
                Confirm
              </Button>
              <Button
                variant="secondary"
                autoFocus
                isDisabled={withdraw.isPending}
                onPress={() => setIsOpen(false)}
              >
                Cancel
              </Button>
            </>
          }
        >
          <p id={messageId}>
            Your application will no longer be considered for assistance. Please
            confirm you no longer need assistance?
          </p>
          {withdraw.error && (
            <p role="alert">
              Could not withdraw the application: {withdraw.error.message}
            </p>
          )}
        </AlertDialog>
      </Modal>
    </>
  );
}
