import { Button } from "@bcgov/design-system-react-components";

import { useSession } from "@/auth/useSession";
import styles from "./DashboardPage.module.css";

export default function DashboardPage() {
    const { user, logout } = useSession();
    const name = user?.name ?? user?.email ?? "there";

    return (
        <div className={styles.page}>
            <div className={styles.actions}>
                <Button variant="primary" onPress={() => logout()}>
                    Log out
                </Button>
            </div>
            <h1>Hello {name}</h1>
        </div>
    );
}
