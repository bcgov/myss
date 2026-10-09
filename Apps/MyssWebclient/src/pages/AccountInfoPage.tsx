import { useAccount } from "@/hooks/useAccount";
import AccountSummary from "@/widgets/account/AccountSummary";
import ContactInformation from "@/widgets/account/ContactInformation";
import NotificationPreferences from "@/widgets/account/NotificationPreferences";
import PinManagement from "@/widgets/account/PinManagement";
import styles from "./AccountInfoPage.module.css";

// The dashboard's Account Info section (MYSS-271): case details, contact
// details, PIN management and notification preferences on one page.
export default function AccountInfoPage() {
  const { data: account, error, isPending } = useAccount();

  return (
    <div className={styles.page}>
      <h1>Account Info</h1>
      {isPending && <output>Loading your account…</output>}
      {error && (
        <p role="alert">Could not load your account: {error.message}</p>
      )}
      {account && (
        <>
          <AccountSummary account={account} />
          <ContactInformation account={account} />
          <PinManagement />
          <NotificationPreferences account={account} />
        </>
      )}
    </div>
  );
}
