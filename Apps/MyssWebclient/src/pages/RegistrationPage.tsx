import styles from "./RegistrationPage.module.css";
import { Link } from "react-router";


export default function RegistrationPage() {
    return (
        <div className={styles.page}>
            <nav aria-label="Breadcrumb">
        <Link to="/">← Home</Link>
        
        </nav>
            <h1>Registration</h1>
        </div>
    );
}
