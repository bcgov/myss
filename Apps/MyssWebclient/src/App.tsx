import { Header, Footer } from "@bcgov/design-system-react-components";
import { Outlet } from "react-router";

import "./App.css";
import AnnouncementBanner from "@/components/layout/AnnouncementBanner";
import { useApiAuth } from "@/auth/useApiAuth";
import { useIdleLogout } from "@/auth/useIdleLogout";

// Shared layout for every route: BC Gov design-system header, the site-wide
// announcement banner, the routed page (<Outlet />), and the BC Gov footer.
// Also mounts the two app-wide auth concerns: the API token bridge and the
// idle-logout timer (RULE-IDA-07).
function App() {
    useApiAuth();
    const { warning: idleWarning } = useIdleLogout();

    return (
        <>
            <Header title="My Self Serve" />
            {idleWarning && (
                <AnnouncementBanner>
                    You&rsquo;ve been inactive for a while and will be signed out
                    soon. Move your mouse or press a key to stay signed in.
                </AnnouncementBanner>
            )}
            <main id="main-content">
                <Outlet />
            </main>
            <Footer />
        </>
    );
}

export default App;
