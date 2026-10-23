import { Button } from "@bcgov/design-system-react-components";

import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import pageStyles from "./Page.module.css";

// Where sign-out and idle logout land. Not behind RequireIdir on purpose:
// signing in again is the user's choice, never automatic, so a logout on an
// unattended workstation stays a logout.
export default function SignedOutPage() {
    const { login } = useSession();

    return (
        <div className={pageStyles.page}>
            <h1>You are signed out</h1>
            <p>
                You have signed out of My Self Serve Administration, or were
                signed out after a period of inactivity.
            </p>
            <div>
                <Button variant="primary" onPress={() => login(paths.home)}>
                    Sign in with IDIR
                </Button>
            </div>
        </div>
    );
}
