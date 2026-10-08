import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { useNavigate } from "react-router";
import { Accordion, AccordionGroup, Button, InlineAlert } from "@bcgov/design-system-react-components";
import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import RegistrationForm from "@/widgets/RegistrationForm";
import AboutMySS from "@/widgets/home/AboutMySS";
import styles from "./RegistrationPage.module.css";

/**
 * Shown in place of the form once the API accepts the registration. The
 * heading takes focus so a screen-reader user hears that the page changed.
 */
function RegistrationComplete() {
    const navigate = useNavigate();
    const headingRef = useRef<HTMLHeadingElement>(null);

    useEffect(() => {
        headingRef.current?.focus();
    }, []);

    return (
        <div className={styles.page}>
            <h1 ref={headingRef} tabIndex={-1} className={styles.title}>
                Account registration complete
            </h1>
            <InlineAlert
                variant="success"
                role="status"
                title="Your account is being prepared. This should take less than 5 minutes."
            />
            <p>Please try signing in again shortly. Thank you for your patience.</p>
            <div>
                <Button variant="secondary" onPress={() => navigate(paths.home)}>
                    Return to the MySS homepage
                </Button>
            </div>
        </div>
    );
}

export default function RegistrationPage() {
    const { hasProfile, isAuthenticated, isLoading, isMeLoading, login, logout, user } = useSession();
    const navigate = useNavigate();
    // Set the moment the API accepts the registration. The refreshed /me then
    // reports a profile, and without this the redirect below would replace the
    // confirmation with the dashboard.
    const [registered, setRegistered] = useState(false);
    const handleRegistered = useCallback(() => setRegistered(true), []);

    useEffect(() => {
        if (isAuthenticated && !isMeLoading && hasProfile && !registered) {
            void navigate(paths.dashboard, { replace: true });
        }
    }, [hasProfile, isAuthenticated, isMeLoading, navigate, registered]);

    if (registered) return <RegistrationComplete />;

    // A BCeID token carries a BCeID GUID; a citizen registering without one
    // signed in with BC Services Card (IDIR users do not register).
    const signedInWith = user?.bceidGuid ? "BCeID" : "BC Services Card";

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
                <InlineAlert variant="success">
                    {/* InlineAlert renders `title` only when it has no
                        children, so the title uses the component's own
                        markup (the target of its aria-labelledby). */}
                    <span className="title" id="alert-title">
                        Signed in successfully
                    </span>
                    <span className="description">
                        You’re signed in with your {signedInWith}.
                        <br />
                        We need some additional information to create your MySS account.
                    </span>
                </InlineAlert>
                <h1 className={styles.title}>Create your MySS account</h1>
                <RegistrationForm
                    identity={user ?? {}}
                    onRegistered={handleRegistered}
                    onCancel={logout}
                />
            </>
        );
    } else {
        body = (
            <>
                <h1 className={styles.title}>Create your MySS account</h1>
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
                    <Accordion label="I don’t have a BC Services Card or BCeID">
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
        );
    }

    return <div className={styles.page}>{body}</div>;
}
