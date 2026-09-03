import { Callout } from "@bcgov/design-system-react-components";
import { Link } from "@bcgov/design-system-react-components";

import styles from "./ChequeCalendar.module.css";

const nextChequeDate = "Wednesday, July 29";
const daysFromNow = "17 days from now";
const chequeScheduleUrl =
    "https://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/payment-dates";

export default function ChequeCalendar() {
    return (
        <section className={styles.wrapper} aria-label="Cheque issue information">
            <Callout title="Payment information">
                <div className={styles.content}>
                    <div className={styles.dateCard}>
                        <p className={styles.dateCardHeader}>
                            Next cheque issue date
                        </p>
                        <b>{nextChequeDate}</b>
                        <p>{daysFromNow}</p>
                    </div>
                    <div className={styles.information}>
                        <h2>Payment information</h2>
                        <p>
                            The upcoming cheque issue date is <b>{nextChequeDate}</b>.
                        </p>
                        <p>
                            Learn more about cheque issue dates and see{" "}
                            <Link href={chequeScheduleUrl} size="large">
                                the full schedule for 2027.
                            </Link>
                        </p>
                    </div>
                </div>
            </Callout>
        </section>
    );
}
