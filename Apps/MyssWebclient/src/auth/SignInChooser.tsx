// PERMANENT (depends only on useSession). The in-app identity-provider chooser.
// RULE-IDA-08: Business BCeID is intentionally NOT offered on the sign-in path.
// Nor is IDIR: Ministry staff sign in on the admin site (MyssAdminWebclient).
// Used by the /auth/login route and as the RequireAuth fallback.

import { Button } from "@bcgov/design-system-react-components";

import { useSession } from "./useSession";
import styles from "./SignInChooser.module.css";

export default function SignInChooser({
    returnTo,
}: Readonly<{
    /** Where to land after signing in; the app's default page when absent. */
    returnTo?: string;
}>) {
    const { login } = useSession();
    return (
        <div className={styles.chooser}>
            <p className={styles.prompt}>Sign in with:</p>
            <Button
                variant="primary"
                size="large"
                onPress={() => login("bcServicesCard", returnTo)}
            >
                BC Services Card
            </Button>
            <Button
                variant="secondary"
                size="large"
                onPress={() => login("bceid", returnTo)}
            >
                Basic BCeID
            </Button>
        </div>
    );
}
