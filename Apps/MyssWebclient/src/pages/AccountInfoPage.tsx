import { InlineAlert } from "@bcgov/design-system-react-components";
import { useEffect, useState } from "react";
import { useSearchParams } from "react-router";

import { useAccount } from "@/hooks/useAccount";
import { PIN_RESET_DONE_PARAM } from "@/routes/paths";
import AccountSummary from "@/widgets/account/AccountSummary";
import ContactInformation from "@/widgets/account/ContactInformation";
import NotificationPreferences from "@/widgets/account/NotificationPreferences";
import PinManagement from "@/widgets/account/PinManagement";
import styles from "./AccountInfoPage.module.css";

// The dashboard's Account Info section (MYSS-271): case details, contact
// details, PIN management (MYSS-258) and notification preferences on one page.

export default function AccountInfoPage() {
  const { data: account, error, isPending } = useAccount();
  const [searchParams, setSearchParams] = useSearchParams();
  // Read once, then the marker leaves the address, so a refresh or a shared
  // link does not say it again.
  const [pinWasReset] = useState(
    () => searchParams.get(PIN_RESET_DONE_PARAM) === "done",
  );
  useEffect(() => {
    if (!searchParams.has(PIN_RESET_DONE_PARAM)) return;
    const next = new URLSearchParams(searchParams);
    next.delete(PIN_RESET_DONE_PARAM);
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

  return (
    <div className={styles.page}>
      <h1>Account Info</h1>
      {pinWasReset && (
        <output>
          <InlineAlert
            variant="success"
            description="Your PIN has been reset."
          />
        </output>
      )}
      {isPending && <output>Loading your account…</output>}
      {error && (
        <p role="alert">Could not load your account: {error.message}</p>
      )}
      {account && (
        <>
          <AccountSummary account={account} />
          <ContactInformation account={account} />
          <PinManagement account={account} />
          <NotificationPreferences account={account} />
        </>
      )}
    </div>
  );
}
