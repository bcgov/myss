import { Button, Header, Footer } from "@bcgov/design-system-react-components";
import { Outlet, useLocation } from "react-router";

import "./App.css";
import AnnouncementBanner from "@/widgets/layout/AnnouncementBanner";
import { paths } from "@/routes/paths";
import { useApiAuth } from "@/auth/useApiAuth";
import { useIdleLogout } from "@/auth/useIdleLogout";
import { useErrorMessageCatalogue } from "@/hooks/useErrorMessages";

// Shared layout for every route: BC Gov design-system header, the site-wide
// announcement banner, the routed page (<Outlet />), and the BC Gov footer.
// Also mounts the app-wide concerns: the API token bridge, the idle-logout
// timer (RULE-IDA-07), and the error message catalogue every form reads.
function App() {
    useApiAuth();
    useErrorMessageCatalogue();
    const { warning: idleWarning, extendSession } = useIdleLogout();
    // The eligibility estimator has no footer, so hide
    // the shared BC Gov footer on that route only — every other page keeps it.
    const { pathname } = useLocation();
    const hideFooter = pathname === paths.eligibilityEstimator;

    return (
        <>
            <Header title="My Self Serve" />
            {idleWarning && (
                <AnnouncementBanner>
                    You&rsquo;ve been inactive for a while and will be signed
                    out soon.{" "}
                    <Button onPress={extendSession}>Stay signed in</Button>
                </AnnouncementBanner>
            )}
            <main id="main-content">
                <Outlet />
            </main>
            {!hideFooter && <Footer />}
        </>
    );
}

export default App;
