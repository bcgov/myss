// The in-app sign-in chooser page (paths.signIn). Replaces the old external
// redirect. Already-authenticated users are sent to their dashboard page.

import { useEffect } from "react";
import { useNavigate } from "react-router";

import { useSession } from "@/auth/useSession";
import SignInChooser from "@/auth/SignInChooser";
import { paths } from "@/routes/paths";

export default function SignInPage() {
    const { hasProfile, isAuthenticated, isMeLoading } = useSession();
    const navigate = useNavigate();

    useEffect(() => {
        if (isAuthenticated && hasProfile !== undefined) {
            navigate(hasProfile ? paths.dashboard : paths.register, { replace: true });
        }
    }, [hasProfile, isAuthenticated, navigate]);

    if (isAuthenticated && isMeLoading) {
        return <p role="status">Checking your MySS account…</p>;
    }

    return (
        <div style={{ maxWidth: 820, margin: "0 auto", width: "100%" }}>
            <h1>Sign in to My Self Serve</h1>
            <SignInChooser />
        </div>
    );
}
