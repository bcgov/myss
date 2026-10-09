import { Button } from "@bcgov/design-system-react-components";

import styles from "./AccountSections.module.css";

// PIN management on Account Info. The buttons are in place for the design;
// Change PIN (MYSS-35) and Reset PIN (MYSS-36) are their own stories and
// will give them something to do.
export default function PinManagement() {
  return (
    <section aria-labelledby="pin-management" className={styles.section}>
      <h2 id="pin-management">PIN management</h2>
      <p>
        Choose a 4-digit number that will verify your identity when you sign and
        submit documents online.
      </p>
      <div>
        <Button variant="secondary">Change my PIN</Button>
      </div>
      <p>We can email you a link to reset your 4-digit PIN.</p>
      <div>
        <Button variant="secondary">Reset PIN</Button>
      </div>
    </section>
  );
}
