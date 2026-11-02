import type { ReviewRow } from "@/widgets/registrationSpec";
import styles from "./RegistrationReview.module.css";

// The Step 3 review table (MYSS-295): what the citizen gave in Step 1, as
// they will read it on their account. A real table with header cells, so a
// screen reader announces each value with its label.

export default function RegistrationReview({
  rows,
}: Readonly<{ rows: readonly ReviewRow[] }>) {
  return (
    <table className={styles.review}>
      <caption className={styles.visuallyHidden}>Your information</caption>
      <thead>
        <tr>
          <th scope="col">Information</th>
          <th scope="col">Details</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.key}>
            <th scope="row">{row.label}</th>
            <td>{row.value}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
