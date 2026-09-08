import {
    SvgBcOutlineIcon,
    SvgCheckCircleIcon,
    SvgInfoIcon,
} from "@bcgov/design-system-react-components";

import styles from "./AboutMySS.module.css";

const information = [
    {
        title: "Access assistance online",
        icon: <SvgBcOutlineIcon />,
        text: "Provides online access to income and disability assistance for residents of British Columbia.",
    },
    {
        title: "Apply for assistance",
        icon: <SvgCheckCircleIcon />,
        text: "If you are not currently in receipt of income or disability assistance, MySS will guide you through the application process.",
    },
    {
        title: "Manage your assistance",
        icon: <SvgInfoIcon />,
        text: "If you are currently in receipt of income or disability assistance, MySS will allow you to securely access your current information online. For example, you can view personal messages from the ministry, submit your monthly report, and upload forms.",
    },
];
export default function AboutMySS() {
    return (
        <section aria-labelledby="about-myss-heading">
            <h2 id="about-myss-heading">About MySS</h2>
            <div className={styles.columns}>
                {information.map((item) => (
                    <article key={item.title}>
                        <div className={styles.icon} aria-hidden="true">
                            {item.icon}
                        </div>
                        <h3>{item.title}</h3>
                        <p>{item.text}</p>
                    </article>
                ))}
            </div>
        </section>
    );
}
