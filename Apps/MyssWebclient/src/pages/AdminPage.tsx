import { Link } from "react-router";

import { paths } from "@/routes/paths";
import homeStyles from "./HomePage.module.css";
import styles from "./AdminPage.module.css";

export default function AdminPage() {
    return (
        <div className={homeStyles.page}>
            <header className={styles.heading}>
                <p className={styles.eyebrow}>My Self Serve</p>
                <h1>Administration</h1>
            </header>

            <section aria-labelledby="admin-capabilities">
                <h2 id="admin-capabilities" className={styles.sectionTitle}>
                    Admin capabilities
                </h2>
                <ul className={styles.capabilityList}>
                    <li className={styles.capabilityItem}>
                        <Link to={paths.adminFormManagement}>
                            Form management
                        </Link>
                    </li>
                </ul>
            </section>
        </div>
    );
}
