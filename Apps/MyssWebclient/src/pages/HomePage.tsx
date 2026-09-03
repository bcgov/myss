import AccountPanel from "@/components/home/AccountPanel";
import IntroSection from "@/components/home/IntroSection";
import VideoSection from "@/components/home/VideoSection";
import HowToApply from "../components/home/HowToApply";
import ChequeCalendar from "@/components/home/ChequeCalendar";
import LinkList from "@/components/home/LinkList";
import { commonQuestions, otherResources } from "@/data/homeLinks";
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
            
            <section className={styles.resourcesSection} aria-labelledby="key-resources-heading">
                <h2 id="key-resources-heading">Key resources</h2>
                <div className={styles.resourcesRow}>
                    <div className={styles.videoCol}>
                        <VideoSection />
                    </div>
                    <HowToApply />
                </div>
            </section>

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
