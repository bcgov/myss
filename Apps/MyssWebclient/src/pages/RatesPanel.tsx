import { useEstimatorRates } from "@/hooks/useEligibility";
import {
    ASSET_COLUMNS,
    INCOME_COLUMN_GROUPS,
    INCOME_COLUMNS,
    isNotApplicable,
} from "@/lib/rateColumns";
import styles from "./RatesPanel.module.css";

// Read-only view of the eligibility rate table the estimator computes against
// (income limits by client-type category A–I in three groups, asset ceilings
// A–D). Uses the same anonymous read the citizen estimator uses.

const money = new Intl.NumberFormat("en-CA", {
    style: "currency",
    currency: "CAD",
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
});

/** A couple column at family size 1 has no limit; read out in full by screen readers. */
function NotApplicable() {
    return (
        <>
            <span aria-hidden="true">N/A</span>
            <span className={styles.visuallyHidden}>Not applicable</span>
        </>
    );
}

export default function RatesPanel() {
    const { data: rates, isPending, error } = useEstimatorRates();

    return (
        <section aria-labelledby="rates-heading" className={styles.panel}>
            <h2 id="rates-heading" className={styles.heading}>
                Eligibility rates
            </h2>

            {isPending && <p>Loading rates…</p>}

            {!isPending && error && (
                <p role="alert" className={styles.error}>
                    Could not load the eligibility rates: {error.message}
                </p>
            )}

            {!isPending && !error && rates && (
                <>
                    <p className={styles.effective}>
                        Effective {rates.effectiveDate}
                    </p>

                    <h3 id="income-limits-heading" className={styles.tableTitle}>
                        Monthly income limits
                    </h3>
                    <p className={styles.caption}>By client-type category</p>
                    <div className={styles.tableWrap}>
                        <table
                            aria-labelledby="income-limits-heading"
                            className={styles.table}
                        >
                            <colgroup>
                                <col />
                            </colgroup>
                            {INCOME_COLUMN_GROUPS.map(({ group, span }) => (
                                <colgroup key={group} span={span} />
                            ))}
                            <thead>
                                <tr>
                                    <th
                                        rowSpan={2}
                                        scope="col"
                                        className={styles.rowHeading}
                                    >
                                        Family size
                                    </th>
                                    {INCOME_COLUMN_GROUPS.map(({ group, span }) => (
                                        <th
                                            key={group}
                                            colSpan={span}
                                            scope="colgroup"
                                            className={styles.groupHeading}
                                        >
                                            {group}
                                        </th>
                                    ))}
                                </tr>
                                <tr>
                                    {INCOME_COLUMNS.map(({ letter }) => (
                                        <th key={letter} scope="col">
                                            {letter.toUpperCase()}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {rates.incomeRows.map((row) => (
                                    <tr key={row.familySize}>
                                        <th scope="row">{row.familySize}</th>
                                        {INCOME_COLUMNS.map(({ letter }) => (
                                            <td key={letter}>
                                                {isNotApplicable(row.familySize, letter) ? (
                                                    <NotApplicable />
                                                ) : (
                                                    money.format(row[letter])
                                                )}
                                            </td>
                                        ))}
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    <h3 id="asset-limits-heading" className={styles.tableTitle}>
                        Asset limits
                    </h3>
                    <div className={styles.tableWrap}>
                        <table
                            aria-labelledby="asset-limits-heading"
                            className={styles.table}
                        >
                            <thead>
                                <tr>
                                    {ASSET_COLUMNS.map((letter) => (
                                        <th key={letter} scope="col">
                                            {letter.toUpperCase()}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                <tr>
                                    {ASSET_COLUMNS.map((letter) => (
                                        <td key={letter}>
                                            {money.format(rates.assetLimits[letter])}
                                        </td>
                                    ))}
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </>
            )}
        </section>
    );
}
