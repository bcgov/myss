import { RadioGroup, Radio } from "@bcgov/design-system-react-components";

import styles from "./PwdQuestion.module.css";

interface PwdQuestionProps {
    /** Field name (e.g. "PWD" or "PartnerPWD"). */
    name: string;
    question: string;
}

// Reusable Yes/No question for Persons with Disabilities (PWD) designation.
// Prod shows the question as an <h2> heading with Yes/No radios beneath.
// Placeholder only - selection is not persisted or used in any calculation yet.
export default function PwdQuestion({ name, question }: PwdQuestionProps) {
    return (
        <>
            <h2 className={styles.question}>{question}</h2>
            <RadioGroup name={name} aria-label={question}>
                <Radio value="true">Yes</Radio>
                <Radio value="false">No</Radio>
            </RadioGroup>
        </>
    );
}
