import {
    Button,
    Link as BcLink,
} from "@bcgov/design-system-react-components";
import { useNavigate } from "react-router";

import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import styles from "./AccountPanel.module.css";

// Right-hand account card. Depends only on the useSession seam, so it is
// unchanged when the app moves from Option 1 to Option 2. Signed-in users see a
// welcome + sign out; signed-out users see sign in / create account.
export default function AccountPanel() {
    const { isAuthenticated, user, logout } = useSession();
    const navigate = useNavigate();

    return (
        <aside className={styles.wrapper} aria-label="Account access">
            <div className={styles.card}>
                {isAuthenticated ? (
                    <>
                        <p>
                            <strong>
                                Welcome back
                                {user?.name ? `, ${user.name}` : ""}
                            </strong>
                        </p>
                        <Button
                            variant="primary"
                            size="large"
                            onPress={() => logout()}
                        >
                            Sign out
                        </Button>
                    </>
                ) : (
                    <>
                        <p>
                            <strong>Yes, I have a MySS account</strong>
                        </p>
                        <Button
                            variant="primary"
                            size="large"
                            onPress={() => navigate(paths.signIn)}
                        >
                            Sign in
                        </Button>

                        <p>
                            <strong>No, I do not have a MySS account</strong>
                        </p>
                        <Button
                            variant="secondary"
                            size="large"
                            onPress={() => {
                                window.location.href = paths.register;
                            }}
                        >
                            Create an account
                        </Button>
                    </>
                )}
            </div>

            <div className={styles.card}>
                <BcLink
                    href="http://www2.gov.bc.ca/assets/download/240B1495B3C3497F8153C6D1EC6429B3"
                >
                    How to apply for or return to assistance
                </BcLink>
            </div>
        </aside>
    );
}
