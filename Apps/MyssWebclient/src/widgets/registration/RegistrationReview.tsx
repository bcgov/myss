import styles from "./RegistrationReview.module.css";

// The review table: what the citizen gave, as they will read it on their
// account. A real table with header cells, so a screen reader announces each
// value with its label, and each group's heading with the rows it leads.

/** One row of the table; a list for several values, such as documents. */
export interface ReviewEntry {
  key: string;
  label: string;
  value: string | readonly string[];
}

export interface ReviewGroup {
  heading: string;
  rows: readonly ReviewEntry[];
}

function Row({ row }: Readonly<{ row: ReviewEntry }>) {
  return (
    <tr>
      <th scope="row">{row.label}</th>
      <td>
        {typeof row.value === "string" ? (
          row.value
        ) : (
          <ul className={styles.values}>
            {row.value.map((item, index) => (
              <li key={index}>{item}</li>
            ))}
          </ul>
        )}
      </td>
    </tr>
  );
}

/** Rows alone, or groups of rows; one group draws as rows alone. */
export default function RegistrationReview({
  rows = [],
  groups,
}: Readonly<{
  rows?: readonly ReviewEntry[];
  groups?: readonly ReviewGroup[];
}>) {
  const grouped = groups !== undefined && groups.length > 1;
  const single = groups?.length === 1 ? groups[0].rows : rows;
  return (
    <table className={styles.review}>
      <caption className={styles.visuallyHidden}>Your information</caption>
      <thead>
        <tr>
          <th scope="col">Information</th>
          <th scope="col">Details</th>
        </tr>
      </thead>
      {grouped ? (
        groups.map((group) => (
          <tbody key={group.heading}>
            <tr className={styles.group}>
              <th colSpan={2} scope="rowgroup">
                {group.heading}
              </th>
            </tr>
            {group.rows.map((row) => (
              <Row key={row.key} row={row} />
            ))}
          </tbody>
        ))
      ) : (
        <tbody>
          {single.map((row) => (
            <Row key={row.key} row={row} />
          ))}
        </tbody>
      )}
    </table>
  );
}
