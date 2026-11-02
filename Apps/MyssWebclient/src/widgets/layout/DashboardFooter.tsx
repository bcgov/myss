import styles from "./DashboardFooter.module.css";

// The signed-in pages end with the copyright line alone, in place of the
// full BC Gov footer the public pages show.
export default function DashboardFooter() {
  return (
    <footer className={styles.footer}>
      <p>© {new Date().getFullYear()} Government of British Columbia.</p>
    </footer>
  );
}
