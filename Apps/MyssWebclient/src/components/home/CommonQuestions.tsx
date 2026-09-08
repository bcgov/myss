import {
    Accordion,
    AccordionGroup,
    Link,
} from "@bcgov/design-system-react-components";

import { commonQuestions } from "@/data/homeLinks";
import styles from "./CommonQuestions.module.css";

export default function CommonQuestions() {
    return (
        <div>
            <h2>Common questions:</h2>
            <AccordionGroup className={styles.group}>
                {commonQuestions.map((question) => (
                    <Accordion key={question.href} label={question.label}>
                        <Link
                            href={question.href}
                            size="large"
                            target={question.external ? "_blank" : undefined}
                            rel={
                                question.external
                                    ? "noopener noreferrer"
                                    : undefined
                            }
                        >
                            Learn more
                        </Link>
                    </Accordion>
                ))}
            </AccordionGroup>
        </div>
    );
}
