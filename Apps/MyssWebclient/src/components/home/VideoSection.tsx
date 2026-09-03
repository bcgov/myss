import { homeVideos } from "@/data/homeLinks";
import styles from "./VideoSection.module.css";

// Informational videos 
// Prod separates each block with a horizontal rule; we mirror that here.
// The eligibility link routes internally to the placeholder estimator page.
export default function VideoSection() {
    return (
        <div className={styles.wrapper}>
            {homeVideos.map((video) => (
                <div className={styles.videoItem} key={video.id}>
                    <h4>{video.title}</h4>
                    <div className={styles.videoFrame}>
                        <iframe
                            src={`https://www.youtube.com/embed/${video.id}`}
                            title={video.title}
                            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                            allowFullScreen
                        />
                    </div>
                </div>
            ))}
        </div>
    );
}
