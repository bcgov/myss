import AccountPanel from "@/components/home/AccountPanel";
import IntroSection from "@/components/home/IntroSection";
import VideoSection from "@/components/home/VideoSection";
import ChequeCalendar from "@/components/home/ChequeCalendar";
import LinkList from "@/components/home/LinkList";
import { commonQuestions, otherResources } from "@/data/homeLinks";
import styles from "./HomePage.module.css";

// My Self Serve home page, built with the BC Gov design system. Sections are
// componentised so each can be wired to real data/flows over time.
export default function HomePage() {
    return (
        <div className={styles.page}>
            <h1 className={styles.title}>Welcome to My Self Serve</h1>

            {/* Intro copy + account access side by side. */}
            <div className={styles.introRow}>
                <div className={styles.introText}>
                    <IntroSection />
                </div>
                <AccountPanel />
            </div>

            {/* Videos / eligibility CTA + next cheque calendar. */}
            <div className={styles.mediaRow}>
                <div className={styles.videoCol}>
                    <VideoSection />
                </div>
                <div className={styles.calendarCol}>
                    <ChequeCalendar />
                </div>
            </div>

            {/* Link lists. */}
            <div className={styles.linksCol}>
                <LinkList title="Common questions:" links={commonQuestions} />
                <LinkList
                    title="Links to other resources:"
                    links={otherResources}
                />
            </div>
        </div>
    );
}
