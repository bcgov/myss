import { Button, Header } from "@bcgov/design-system-react-components";
import { Outlet } from "react-router";
import { AnnouncementBanner } from "@myss/web-common/components";

import "./App.css";
import { useApiAuth } from "@/auth/useApiAuth";
import { useIdleLogout } from "@/auth/useIdleLogout";
import { useSession } from "@/auth/useSession";

// Shared layout for every route: the BC Gov design-system header, the idle
// warning, and the routed page (<Outlet />). Also mounts the app-wide concerns:
// the API token bridge and the idle-logout timer (RULE-IDA-07).
//
// No error message catalogue is loaded here: the editor's preview words rule
// failures with the compiled defaults (see README.md).
function App() {
    useApiAuth();
    const { warning: idleWarning, extendSession } = useIdleLogout();
    const { isAuthenticated, logout } = useSession();

    return (
        <>
            <Header title="My Self Serve Administration">
                {isAuthenticated && (
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
            <main id="main-content">
                <Outlet />
            </main>
        </>
    );
}

export default App;
