import { Button } from "@bcgov/design-system-react-components";

import DashboardNav from "./DashboardNav";
import styles from "./PhoneMenu.module.css";

interface PhoneMenuProps {
  id: string;
  /** Called when any section link is chosen, including the current one. */
  onNavigate: () => void;
  onSignOut: () => void;
}

// The dashboard menu on a phone, opened from the header: the same sections
// as the side menu, then Sign out, which a desktop shows in the header.
export default function PhoneMenu({ id, onNavigate, onSignOut }: PhoneMenuProps) {
  return (
    <div id={id} className={styles.menu}>
      <DashboardNav onNavigate={onNavigate} />
      <div className={styles.signOut}>
        <Button variant="secondary" size="medium" onPress={onSignOut}>
          Sign out
        </Button>
      </div>
    </div>
  );
}
