import { useEffect } from "react";
import type { ReactNode } from "react";
import { Link, useNavigate } from "react-router";
import { Button } from "@bcgov/design-system-react-components";
import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import RegistrationForm from "@/widgets/RegistrationForm";
import styles from "./RegistrationPage.module.css";

export default function RegistrationPage() {
    const { hasProfile, isAuthenticated, isLoading, isMeLoading, login, logout, user } = useSession();
    const navigate = useNavigate();

    useEffect(() => {
        if (isAuthenticated && !isMeLoading && hasProfile) {
            void navigate(paths.dashboard, { replace: true });
        }
    }, [hasProfile, isAuthenticated, isMeLoading, navigate]);

    let body: ReactNode;
    if (isLoading) {
        body = (
            <p role="status" aria-live="polite">
                Checking your session…
            </p>
        );
    } else if (isAuthenticated && (isMeLoading || hasProfile === undefined)) {
        body = (
            <p role="status" aria-live="polite">
                Checking your MySS account…
            </p>
        );
    } else if (isAuthenticated) {
        body = (
            <>
                <p>Welcome{user?.name ? `, ${user.name}` : ""}.</p>
                <RegistrationForm />
            </>
        );
    } else {
        body = (
            <>
                <p>
                    Create a MySS account with your BC Services Card or BCeID.
                </p>
                <div className={styles.actions}>
                    <Button
                        variant="primary"
                        size="large"
                        onPress={() => login("bcServicesCard", paths.register)}
                    >
                        BC Services Card
                    </Button>
                    <Button
                        variant="secondary"
                        size="large"
                        onPress={() => login("bceid", paths.register)}
                    >
                        BCeID
                    </Button>
                </div>
            </>
        );
    }

    return (
        <div className={styles.page}>
            <nav aria-label="Breadcrumb">
                <Link to="/">← Home</Link>      
            </nav>
            <div className={styles.heading}>
                <h1>Registration</h1>
                {!isLoading && isAuthenticated && (
                    <Button variant="primary" onPress={() => logout()}>
                        Log out
                    </Button>
                )}
            </div>
            {body}
        </div>
    );
}
