import homeStyles from "./HomePage.module.css";
import styles from "./AdminPage.module.css";

// Placeholder destination for the admin "Form management" capability link.
// No form-spec management functionality is built yet.
export default function FormManagementPage() {
    return (
        <div className={homeStyles.page}>
            <header className={styles.heading}>
                <p className={styles.eyebrow}>My Self Serve</p>
                <h1>Form management</h1>
            </header>
        </div>
    );
}
