import { Button } from "@bcgov/design-system-react-components";
import { useEffect } from "react";
import { useNavigate } from "react-router";

import { useSession } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import styles from "./DashboardPage.module.css";

export default function DashboardPage() {
    const { hasProfile, isMeLoading, profileFirstName, user, logout } = useSession();
    const navigate = useNavigate();
    const name = user?.name ?? user?.email ?? "there";

    useEffect(() => {
        if (!isMeLoading && hasProfile === false) {
            navigate(paths.register, { replace: true });
        }
    }, [hasProfile, isMeLoading, navigate]);

    if (isMeLoading || hasProfile === undefined) {
        return <p role="status">Checking your MySS account…</p>;
    }

    return (
        <div className={styles.page}>
            <div className={styles.actions}>
                <Button variant="primary" onPress={() => logout()}>
                    Log out
                </Button>
            </div>
            <h1>Hello {name}</h1>
            {hasProfile && profileFirstName && (
                <p>Your MySS account profile is registered to {profileFirstName}.</p>
            )}
        </div>
    );
}
