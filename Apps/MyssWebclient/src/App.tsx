import { Button, Header, Footer } from "@bcgov/design-system-react-components";
import { Outlet, useLocation } from "react-router";

import "./App.css";
import AnnouncementBanner from "@/widgets/layout/AnnouncementBanner";
import { paths } from "@/routes/paths";
import { isDashboardPath } from "@/routes/dashboardSections";
import { useApiAuth } from "@/auth/useApiAuth";
import { useIdleLogout } from "@/auth/useIdleLogout";
import { useSession } from "@/auth/useSession";
import { useErrorMessageCatalogue } from "@/hooks/useErrorMessages";

// Shared layout for every route: BC Gov design-system header, the site-wide
// announcement banner, the routed page (<Outlet />), and the BC Gov footer.
// Also mounts the app-wide concerns: the API token bridge, the idle-logout
// timer (RULE-IDA-07), and the error message catalogue every form reads.
function App() {
    useApiAuth();
    useErrorMessageCatalogue();
    const { warning: idleWarning, extendSession } = useIdleLogout();
    const { isAuthenticated, logout } = useSession();
    const { pathname } = useLocation();
    // The signed-in dashboard (MYSS-194) is a full-width frame: its menu runs
    // to the left edge, Sign out moves into the header, and there is no
    // footer. Signed out, a dashboard URL shows the sign-in chooser, which
    // keeps the ordinary page. The eligibility estimator has no footer
    // either; every other page keeps the shared BC Gov footer.
    const isDashboard = isAuthenticated && isDashboardPath(pathname);
    const hideFooter =
        pathname === paths.eligibilityEstimator || isDashboard;

    return (
        <>
            <Header title="My Self Serve">
                {isDashboard && (
                    <div className="app-header-actions">
                        <Button
                            variant="secondary"
                            size="small"
                            onPress={() => logout()}
                        >
                            Sign out
                        </Button>
                    </div>
                )}
            </Header>
            {idleWarning && (
                <AnnouncementBanner>
                    You&rsquo;ve been inactive for a while and will be signed
                    out soon.{" "}
                    <Button onPress={extendSession}>Stay signed in</Button>
                </AnnouncementBanner>
            )}
            <main
                id="main-content"
                className={isDashboard ? "main--dashboard" : undefined}
            >
                <Outlet />
            </main>
            {!hideFooter && <Footer />}
        </>
    );
}

export default App;
