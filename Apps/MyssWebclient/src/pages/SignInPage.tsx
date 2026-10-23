// The in-app sign-in chooser page (paths.signIn). Replaces the old external
// redirect. Already-authenticated users are sent to their dashboard page.
// A page that sends the citizen here may pass a confirmation to show above
// the chooser in the navigation state, as the PIN reset does (MYSS-36).

import { InlineAlert } from "@bcgov/design-system-react-components";
import { useEffect } from "react";
import { useLocation, useNavigate } from "react-router";

import { useSession } from "@/auth/useSession";
import SignInChooser from "@/auth/SignInChooser";
import { paths } from "@/routes/paths";

export default function SignInPage() {
    const { hasProfile, isAuthenticated, isMeLoading } = useSession();
    const navigate = useNavigate();
    const { state } = useLocation();
    const notice = (state as { notice?: unknown } | null)?.notice;

    useEffect(() => {
        if (isAuthenticated && hasProfile !== undefined) {
            void navigate(hasProfile ? paths.dashboard : paths.register, { replace: true });
        }
    }, [hasProfile, isAuthenticated, navigate]);

    if (isAuthenticated && isMeLoading) {
        return <output>Checking your MySS account…</output>;
    }

    return (
        <div style={{ maxWidth: 820, margin: "0 auto", width: "100%" }}>
            <h1>Sign in to My Self Serve</h1>
            {typeof notice === "string" && (
                <output>
                    <InlineAlert variant="success" description={notice} />
                </output>
            )}
            <SignInChooser />
        </div>
    );
}
