import { homeVideos } from "@/data/homeLinks";
import styles from "./VideoSection.module.css";

// Informational videos 
// Rendered as a responsive grid of embedded YouTube videos.
export default function VideoSection() {
    return (
        <div className={styles.wrapper}>
            {homeVideos.map((video) => (
                <div className={styles.videoItem} key={video.id}>
                    <h3>{video.title}</h3>
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
