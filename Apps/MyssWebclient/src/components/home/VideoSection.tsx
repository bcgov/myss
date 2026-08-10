import { Fragment } from "react";
import { Link } from "react-router";

import { homeVideos } from "@/data/homeLinks";
import { paths } from "@/routes/paths";
import styles from "./VideoSection.module.css";

// Informational videos plus the "Estimate your eligibility" call-to-action.
// Prod separates each block with a horizontal rule; we mirror that here.
// The eligibility link routes internally to the placeholder estimator page.
export default function VideoSection() {
    return (
        <div className={styles.wrapper}>
            {homeVideos.map((video) => (
                <Fragment key={video.id}>
                    <div className={styles.videoFrame}>
                        <iframe
                            src={`https://www.youtube.com/embed/${video.id}`}
                            title={video.title}
                            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                            allowFullScreen
                        />
                    </div>
                    <hr className={styles.rule} />
                </Fragment>
            ))}

            <p className={styles.estimatorLink}>
                <Link to={paths.eligibilityEstimator}>
                    Estimate your eligibility before applying for assistance
                    &nbsp;&rsaquo;
                </Link>
            </p>
            <hr className={styles.rule} />
        </div>
    );
}
