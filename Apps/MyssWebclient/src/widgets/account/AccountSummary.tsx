import type { AccountPayload } from "@/hooks/useAccount";
import styles from "./AccountSummary.module.css";

// The case lines at the top of Account Info (MYSS-271). The case number and
// family members are placeholders until ICM and MIS are connected; a citizen
// with no case then has no case number line, and one alone on their case has
// no family line.
export default function AccountSummary({
  account,
}: Readonly<{ account: AccountPayload }>) {
  return (
    <dl className={styles.summary}>
      {account.caseNumber && (
        <div className={styles.row}>
          <dt>Case Number:</dt>
          <dd>{account.caseNumber}</dd>
        </div>
      )}
      <div className={styles.row}>
        <dt>Client Name:</dt>
        <dd>{account.clientName}</dd>
      </div>
      {account.familyMembers.length > 0 && (
        <div className={styles.row}>
          <dt>Case Family Members:</dt>
          <dd>{account.familyMembers.join(", ")}</dd>
        </div>
      )}
    </dl>
  );
}
