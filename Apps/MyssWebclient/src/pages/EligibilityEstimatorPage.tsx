import { useState } from "react";
import {
    Button,
    RadioGroup,
    Radio,
    NumberField,
} from "@bcgov/design-system-react-components";

import PwdQuestion from "@/components/eligibility/PwdQuestion";
import FinancialQuestions from "@/components/eligibility/FinancialQuestions";
import {
    relationshipStatusOptions,
    partneredStatuses,
} from "@/data/eligibilityOptions";
import styles from "./EligibilityEstimatorPage.module.css";

// Pre-Eligibility Estimator, built with the BC Gov design system.
// Prod renders each question as an <h2> heading with the form control beneath;
// we mirror that structure while keeping the design-system controls (the
// visible <h2> labels the control, which itself gets an aria-label).
// This is a UI placeholder: the "Get Estimate" button does nothing and no
// estimate is calculated. Only the spouse-section show/hide behaviour is wired
// up so the form reads correctly. Calculation logic comes in a later step.
export default function EligibilityEstimatorPage() {
    const [relationshipStatus, setRelationshipStatus] = useState<string>("");
    const showSpouseSections = partneredStatuses.includes(relationshipStatus);

    return (
        <div className={styles.page}>
            <h1>Estimate your Eligibility for Assistance</h1>

            <form
                className={styles.form}
                onSubmit={(e) => {
                    // Placeholder: no estimate is calculated yet.
                    e.preventDefault();
                }}
            >
                <section className={styles.section}>
                    <h2 className={styles.question}>
                        What is your relationship status?
                    </h2>
                    <RadioGroup
                        name="RelationshipStatus"
                        aria-label="What is your relationship status?"
                        value={relationshipStatus}
                        onChange={setRelationshipStatus}
                    >
                        {relationshipStatusOptions.map((option) => (
                            <Radio key={option.value} value={option.value}>
                                {option.label}
                            </Radio>
                        ))}
                    </RadioGroup>
                </section>

                <section className={styles.section}>
                    <h2 className={styles.question}>
                        How many dependent children under the age of 19 live
                        with you?
                    </h2>
                    <div className={styles.narrowField}>
                        <NumberField
                            name="DependentChildren"
                            aria-label="How many dependent children under the age of 19 live with you?"
                            defaultValue={0}
                            minValue={0}
                            maxValue={20}
                        />
                    </div>
                </section>

                <section className={styles.section}>
                    <PwdQuestion
                        name="PWD"
                        question="Do you plan to apply for the Persons with Disabilities (PWD) designation?"
                    />
                </section>

                {showSpouseSections && (
                    <section className={styles.section}>
                        <PwdQuestion
                            name="PartnerPWD"
                            question="Does your spouse plan to apply for the Persons with Disabilities (PWD) designation?"
                        />
                    </section>
                )}

                <section className={styles.section}>
                    <h2 className={styles.question}>
                        What is the value of your assets and income?
                    </h2>
                    <FinancialQuestions who="you" />
                </section>

                {showSpouseSections && (
                    <section className={styles.section}>
                        <h2 className={styles.question}>
                            What is the value of your spouse&apos;s assets and
                            income?
                        </h2>
                        <FinancialQuestions who="spouse" />
                    </section>
                )}

                <div className={styles.actions}>
                    <Button variant="primary" size="large" type="submit">
                        Get Estimate
                    </Button>
                </div>
            </form>
        </div>
    );
}
