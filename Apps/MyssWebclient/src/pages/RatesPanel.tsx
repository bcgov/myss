import { useEstimatorRates } from "@/hooks/useEligibility";
import styles from "./RatesPanel.module.css";

// Read-only view of the eligibility rate table the estimator computes against
// (income limits by client-type category A–E, asset ceilings A–D). Uses the
// same anonymous read the citizen estimator uses; no edit controls — editing
// rates is separate, later work.

const money = new Intl.NumberFormat("en-CA", {
    style: "currency",
    currency: "CAD",
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
});

const CATEGORIES = ["a", "b", "c", "d", "e"] as const;

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

                    <h3 className={styles.tableTitle}>Monthly income limits</h3>
                    <p className={styles.caption}>By client-type category</p>
                    <div className={styles.tableWrap}>
                        <table className={styles.table}>
                            <thead>
                                <tr>
                                    <th scope="col">Family size</th>
                                    {CATEGORIES.map((c) => (
                                        <th key={c} scope="col">
                                            {c.toUpperCase()}
                                        </th>
                                    ))}
                                </tr>
                            </thead>
                            <tbody>
                                {rates.incomeRows.map((row) => (
                                    <tr key={row.familySize}>
                                        <th scope="row">{row.familySize}</th>
                                        {CATEGORIES.map((c) => (
                                            <td key={c}>{money.format(row[c])}</td>
                                        ))}
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>

                    <h3 className={styles.tableTitle}>Asset limits</h3>
                    <div className={styles.tableWrap}>
                        <table className={styles.table}>
                            <thead>
                                <tr>
                                    <th scope="col">A</th>
                                    <th scope="col">B</th>
                                    <th scope="col">C</th>
                                    <th scope="col">D</th>
                                </tr>
                            </thead>
                            <tbody>
                                <tr>
                                    <td>{money.format(rates.assetLimits.a)}</td>
                                    <td>{money.format(rates.assetLimits.b)}</td>
                                    <td>{money.format(rates.assetLimits.c)}</td>
                                    <td>{money.format(rates.assetLimits.d)}</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                </>
            )}
        </section>
    );
}
