import { Header, Footer } from "@bcgov/design-system-react-components";
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
            <AnnouncementBanner>
                Please call us at{" "}
                <a href="tel:+18668660800">1-866-866-0800</a> if you need help
                registering for or accessing My Self Serve
            </AnnouncementBanner>
            {idleWarning && (
                <AnnouncementBanner>
                    You&rsquo;ve been inactive for a while and will be signed out
                    soon. Move your mouse or press a key to stay signed in.
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
