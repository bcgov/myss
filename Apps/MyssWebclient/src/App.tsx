import { useId, useRef } from "react";
import { Button, Header, Footer } from "@bcgov/design-system-react-components";
import { Outlet, useLocation } from "react-router";
import { AnnouncementBanner } from "@myss/web-common/components";

import "./App.css";
import { paths } from "@/routes/paths";
import { isDashboardPath } from "@/routes/dashboardSections";
import { getDashboardAccess } from "@/auth/dashboardAccess";
import { useApiAuth } from "@/auth/useApiAuth";
import { useIdleLogout } from "@/auth/useIdleLogout";
import { useSession } from "@/auth/useSession";
import { useErrorMessageCatalogue } from "@/hooks/useErrorMessages";
import { usePhoneLayout } from "@/hooks/usePhoneLayout";
import { usePhoneMenu } from "@/hooks/usePhoneMenu";
import PhoneMenu from "@/widgets/layout/PhoneMenu";

// Shared layout for every route: BC Gov design-system header, the site-wide
// announcement banner, the routed page (<Outlet />), and the BC Gov footer.
// Also mounts the app-wide concerns: the API token bridge, the idle-logout
// timer (RULE-IDA-07), and the error message catalogue every form reads.
function App() {
    useApiAuth();
    useErrorMessageCatalogue();
    const { warning: idleWarning, extendSession } = useIdleLogout();
    const session = useSession();
    const { isAuthenticated, logout } = session;
    const { pathname } = useLocation();
    const isPhoneLayout = usePhoneLayout();
    // The signed-in dashboard is a full-width frame: its menu runs to the
    // left edge, Sign out moves into the header, and there is no footer. On a
    // phone the header has a Menu button instead, once the account is known
    // to belong on the dashboard; until then it keeps Sign out, so a lookup
    // that never resolves still leaves a way out. Signed out, a dashboard URL
    // shows the sign-in chooser, which keeps the ordinary page. The
    // eligibility estimator has no footer either; every other page keeps the
    // shared BC Gov footer.
    const isDashboard = isAuthenticated && isDashboardPath(pathname);
    const hideFooter =
        pathname === paths.eligibilityEstimator || isDashboard;
    const hasPhoneMenu =
        isDashboard &&
        isPhoneLayout &&
        getDashboardAccess(session) === "ready";
    const menuButtonRef = useRef<HTMLDivElement>(null);
    const mainRef = useRef<HTMLElement>(null);
    const menu = usePhoneMenu(hasPhoneMenu, menuButtonRef, mainRef);
    const menuId = useId();

    return (
        <>
            <Header title="My Self Serve">
                {hasPhoneMenu && (
                    <div className="app-header-actions" ref={menuButtonRef}>
                        <Button
                            variant="secondary"
                            size="medium"
                            aria-expanded={menu.isOpen}
                            aria-controls={menu.isOpen ? menuId : undefined}
                            onPress={menu.toggle}
                        >
                            {menu.isOpen ? "Close Menu" : "Menu"}
                        </Button>
                    </div>
                )}
                {isDashboard && !hasPhoneMenu && (
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
            {menu.isOpen && (
                <PhoneMenu
                    id={menuId}
                    onNavigate={menu.close}
                    onSignOut={() => logout()}
                />
            )}
            <main
                id="main-content"
                ref={mainRef}
                tabIndex={-1}
                hidden={menu.isOpen}
                className={isDashboard ? "main--dashboard" : undefined}
            >
                <Outlet />
            </main>
            {!hideFooter && <Footer />}
        </>
    );
}

export default App;
