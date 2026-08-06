import type { ReactNode } from "react";

import styles from "./AnnouncementBanner.module.css";

interface AnnouncementBannerProps {
    children: ReactNode;
}

// Site-wide notice strip shown under the header (mirrors the prod
// "call us for help" announcement). Purely presentational.
export default function AnnouncementBanner({
    children,
}: AnnouncementBannerProps) {
    return (
        <div
            className={styles.banner}
            role="complementary"
            aria-label="Announcement"
        >
            <div className={styles.content}>
                <span className={styles.icon} aria-hidden="true">
                    &#9888;
                </span>
                <span>{children}</span>
            </div>
        </div>
    );
}
