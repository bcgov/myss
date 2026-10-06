import { Checkbox, InlineAlert } from "@bcgov/design-system-react-components";

import {
  useUpdateNotificationPreferences,
  type AccountPayload,
} from "@/hooks/useAccount";
import styles from "./AccountSections.module.css";

// Notification preferences on Account Info (MYSS-271). Saved as soon as the
// box changes. Only the flag is stored for now: nothing sends the reminder yet.
export default function NotificationPreferences({
  account,
}: Readonly<{ account: AccountPayload }>) {
  const save = useUpdateNotificationPreferences();

  return (
    <section
      aria-labelledby="notification-preferences"
      className={styles.section}
    >
      <h2 id="notification-preferences">Notification Preferences</h2>
      <Checkbox
        isSelected={account.monthlyReportReminder}
        isDisabled={save.isPending}
        onChange={(selected) => save.mutate(selected)}
      >
        Please send me a reminder message when the online monthly report is
        available.
      </Checkbox>
      <output>{save.isSuccess && "Your preference has been saved."}</output>
      {save.error && (
        <InlineAlert
          variant="danger"
          role="alert"
          description={`Your preference was not saved. ${save.error.message}`}
        />
      )}
    </section>
  );
}
