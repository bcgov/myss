// The in-app sign-in chooser page (paths.signIn). Replaces the old external
// redirect. Already-authenticated users are sent home.

import { useEffect } from "react";
import { useNavigate } from "react-router";

import { useSession } from "@/auth/useSession";
import SignInChooser from "@/auth/SignInChooser";
import { paths } from "@/routes/paths";

export default function SignInPage() {
    const { isAuthenticated } = useSession();
    const navigate = useNavigate();

    useEffect(() => {
        if (isAuthenticated) navigate(paths.home, { replace: true });
    }, [isAuthenticated, navigate]);

    return (
        <div style={{ maxWidth: 820, margin: "0 auto", width: "100%" }}>
            <h1>Sign in to My Self Serve</h1>
            <SignInChooser />
        </div>
    );
}
