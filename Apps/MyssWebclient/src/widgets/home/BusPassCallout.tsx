import { Callout, Link } from "@bcgov/design-system-react-components";

import { paths } from "@/routes/paths";
import styles from "./BusPassCallout.module.css";

const busPassProgramUrl =
    "https://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/bus-pass";

export default function BusPassCallout() {
    return (
        <section className={styles.band} aria-label="BC Bus Pass">
            <div className={styles.column}>
                <Callout variant="lightBlue">
                    <div className={styles.content}>
                        <h2>Need a bus pass?</h2>
                        <p>
                            Apply for a bus pass online. Learn more about the{" "}
                            <Link href={busPassProgramUrl}>bus pass program</Link>.
                        </p>
                        <Link href={paths.busPass} isButton buttonVariant="secondary">
                            Apply for a bus pass
                        </Link>
                    </div>
                </Callout>
            </div>
        </section>
    );
}
