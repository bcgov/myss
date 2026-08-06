import styles from "./ChequeCalendar.module.css";

const WEEKDAYS = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];

interface DayCell {
    day: number;
    muted: boolean; // adjacent-month day
}

// Static "next cheque issue" calendar placeholder. In prod this is driven by
// /Home/ChequeCalendar; here it renders a fixed month so the layout and
// styling match. Adjacent-month days are shown greyed out (like prod), today
// is underlined, and the issue day is highlighted. Wire to the API later.
export default function ChequeCalendar() {
    // Placeholder data - July 2026 (starts on a Wednesday, 31 days).
    const leading = 3; // Sun 28, Mon 29, Tue 30 (June)
    const daysInMonth = 31;
    const prevMonthDays = 30; // June
    const issueDay = 29;
    const today = 17;

    const cells: DayCell[] = [
        ...Array.from({ length: leading }, (_, i) => ({
            day: prevMonthDays - leading + 1 + i,
            muted: true,
        })),
        ...Array.from({ length: daysInMonth }, (_, i) => ({
            day: i + 1,
            muted: false,
        })),
    ];
    let next = 1;
    while (cells.length % 7 !== 0) {
        cells.push({ day: next++, muted: true });
    }

    return (
        <div
            className={styles.wrapper}
            role="complementary"
            aria-label="Next cheque issue"
        >
            <div className={styles.header}>Next cheque issue: Wed, Jul 29</div>
            <div className={styles.monthNav}>
                <button type="button" aria-label="Previous month" disabled>
                    &lsaquo;
                </button>
                <strong>July 2026</strong>
                <button type="button" aria-label="Next month" disabled>
                    &rsaquo;
                </button>
            </div>
            <table className={styles.calendar} aria-label="Cheque issue calendar">
                <thead>
                    <tr>
                        {WEEKDAYS.map((day) => (
                            <th key={day} scope="col">
                                {day}
                            </th>
                        ))}
                    </tr>
                </thead>
                <tbody>
                    {Array.from({ length: cells.length / 7 }, (_, row) => (
                        <tr key={row}>
                            {cells
                                .slice(row * 7, row * 7 + 7)
                                .map((cell, col) => {
                                    const isIssue =
                                        !cell.muted && cell.day === issueDay;
                                    const isToday =
                                        !cell.muted && cell.day === today;
                                    return (
                                        <td
                                            key={col}
                                            className={[
                                                cell.muted ? styles.muted : "",
                                                isIssue ? styles.issueDay : "",
                                            ]
                                                .filter(Boolean)
                                                .join(" ")}
                                        >
                                            {isToday ? (
                                                <strong>
                                                    <u>{cell.day}</u>
                                                </strong>
                                            ) : (
                                                cell.day
                                            )}
                                        </td>
                                    );
                                })}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}
