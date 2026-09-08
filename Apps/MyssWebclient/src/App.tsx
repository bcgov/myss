import { Button, Header, Footer } from "@bcgov/design-system-react-components";
import { Outlet, useLocation } from "react-router";

import "./App.css";
import AnnouncementBanner from "@/components/layout/AnnouncementBanner";
import { paths } from "@/routes/paths";
import { useApiAuth } from "@/auth/useApiAuth";
import { useIdleLogout } from "@/auth/useIdleLogout";

// Shared layout for every route: BC Gov design-system header, the site-wide
// announcement banner, the routed page (<Outlet />), and the BC Gov footer.
// Also mounts the two app-wide auth concerns: the API token bridge and the
// idle-logout timer (RULE-IDA-07).
function App() {
    useApiAuth();
    const { warning: idleWarning } = useIdleLogout();
    // The eligibility estimator (MYSS-169 / 0901 design) has no footer, so hide
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
            {!hideFooter && <Footer hideAcknowledgement />}
        </>
    );
}

export default App;
