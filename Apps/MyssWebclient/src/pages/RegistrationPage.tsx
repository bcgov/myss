import { Link } from "react-router";
import { useEffect } from "react";
import { useNavigate } from "react-router";
import {
    Accordion,
    AccordionGroup,
    Button,
} from "@bcgov/design-system-react-components";
import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import RegistrationForm from "@/widgets/RegistrationForm";
import AboutMySS from "@/widgets/home/AboutMySS";
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
            <div>
                <h1>Create your MySS account</h1>
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
                        To create your MySS account, you will need to first sign in with a B.C. government ID. You can use your BC Services Card or BCeID. <br />
                        After signing in you can start your MySS registration.
                    </p>
                    <div className={styles.actions}>
                        <Button
                            variant="primary"
                            size="large"
                            onPress={() => login("bcServicesCard", paths.register)}
                        >
                            Register with BC Services Card
                        </Button>
                        <Button
                            variant="secondary"
                            size="large"
                            onPress={() => login("bceid", paths.register)}
                        >
                            Register with Basic BCeID
                        </Button>
                    </div>
                    <p>
                        <strong>Notice:</strong> If you have used MySS previously, do not register for a new account, PLACEHOLDER TEXT. ADD REAL TEXT.
                    </p>
                    <h2>MySS account Help</h2>
                    <AccordionGroup>
                        <Accordion label="I don’t have a BC Service Card or BCeID">
                            <p>Information coming soon.</p>
                        </Accordion>
                        <Accordion label="Why do I need to log in with my government ID?">
                            <p>Information coming soon.</p>
                        </Accordion>
                        <Accordion label="How do I log into my previous MySS account?">
                            <p>Information coming soon.</p>
                        </Accordion>
                    </AccordionGroup>
                    <section className={styles.aboutBand}>
                        <AboutMySS />
                    </section>
                </>
            )}
        </div>
    );
}
