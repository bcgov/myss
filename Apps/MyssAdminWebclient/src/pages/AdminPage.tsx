import { Link } from "react-router";

import { useRoles } from "@/auth/useRoles";
import { paths } from "@/routes/paths";
import pageStyles from "./Page.module.css";
import styles from "./AdminPage.module.css";

// The landing page lists what the signed-in user may use: form management for
// any IDIR user, and the application review queue only for a caller whose
// roles allow it. The roles come from /auth/me, so that link appears once they
// have arrived; the review pages check again (RequireWorker), and the API is
// the real gate.
export default function AdminPage() {
    const { canReview } = useRoles();

    return (
        <div className={pageStyles.page}>
            <header className={styles.heading}>
                <p className={styles.eyebrow}>My Self Serve</p>
                <h1>Administration</h1>
            </header>

            <section aria-labelledby="admin-capabilities">
                <h2 id="admin-capabilities" className={styles.sectionTitle}>
                    Admin capabilities
                </h2>
                <ul className={styles.capabilityList}>
                    {canReview && (
                        <li className={styles.capabilityItem}>
                            <Link to={paths.applications}>
                                Applications to review
                            </Link>
                        </li>
                    )}
                    <li className={styles.capabilityItem}>
                        <Link to={paths.formManagement}>
                            Form management
                        </Link>
                    </li>
                </ul>
            </section>
        </div>
    );
}
