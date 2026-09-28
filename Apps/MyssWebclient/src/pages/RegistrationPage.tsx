import { Link } from "react-router";
import { useEffect } from "react";
import { useNavigate } from "react-router";
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
            navigate(paths.dashboard, { replace: true });
        }
    }, [hasProfile, isAuthenticated, isMeLoading, navigate]);

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
            {isLoading ? (
                <p role="status" aria-live="polite">
                    Checking your session…
                </p>
            ) : isAuthenticated && (isMeLoading || hasProfile === undefined) ? (
                <p role="status" aria-live="polite">
                    Checking your MySS account…
                </p>
            ) : isAuthenticated ? (
                <>
                    <p>Welcome{user?.name ? `, ${user.name}` : ""}.</p>
                    <RegistrationForm />
                </>
            ) : (
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
            )}
        </div>
    );
}
