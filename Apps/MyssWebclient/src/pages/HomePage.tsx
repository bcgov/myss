import AccountPanel from "@/components/home/AccountPanel";
import AboutMySS from "@/components/home/AboutMySS";
import CommonQuestions from "@/components/home/CommonQuestions";
import IntroSection from "@/components/home/IntroSection";
import VideoSection from "@/components/home/VideoSection";
import HowToApply from "../components/home/HowToApply";
import ChequeCalendar from "@/components/home/ChequeCalendar";
import LinkList from "@/components/home/LinkList";
import { otherResources } from "@/data/homeLinks";
import styles from "./HomePage.module.css";

// My Self Serve home page, built with the BC Gov design system. Sections are
// componentised so each can be wired to real data/flows over time.
export default function HomePage() {
    return (
        <div className={styles.page}>
            <section className={styles.introBand}>
                <div className={styles.introBandContent}>
                    <h1>My Self Serve (MySS)</h1>

                    {/* Intro + account access, side by side. */}
                    <div className={styles.introRow}>
                        <div className={styles.introText}>
                            <IntroSection />
                        </div>
                        <AccountPanel />
                    </div>
                </div>
            </section>

            {/* Next cheque calendar. */}
            <section className={styles.calendarBand}>
                <div className={styles.calendarCol}>
                    <ChequeCalendar />
                </div>
            </section>
            
            {/* Key resources. media + how-to, side-by-side. */}
            <section className={styles.resourcesSection} aria-labelledby="key-resources-heading">
                <h2 id="key-resources-heading">Key resources</h2>
                <div className={styles.resourcesRow}>
                    <div className={styles.videoCol}>
                        <VideoSection />
                    </div>
                    <HowToApply />
                </div>
            </section>

            <section className={styles.faqSection}>
                <div className={styles.faqRow}>
                    <CommonQuestions />
                    <LinkList
                        title="Links to other resources:"
                        links={otherResources}
                    />
                </div>
            </section>

            <AboutMySS />
        </div>
    );
}
