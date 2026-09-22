import type { ResourceLink } from "@/data/homeLinks";
import styles from "./LinkList.module.css";
import {Link} from "@bcgov/design-system-react-components";

interface LinkListProps {
    title: string;
    links: ResourceLink[];
}

// Reusable titled list of links, used for "Common questions" and
// "Links to other resources".
export default function LinkList({ title, links }: LinkListProps) {
    return (
        <nav aria-label={title}>
            <h2>{title}</h2>
            <ul className={styles.list}>
                {links.map((link) => (
                    <li key={link.href}>
                        <Link
                            href={link.href}
                            size="large"
                            target={link.external ? "_blank" : undefined}
                            rel={
                                link.external
                                    ? "noopener noreferrer"
                                    : undefined
                            }
                        >
                            {link.label}
                        </Link>
                    </li>
                ))}
            </ul>
        </nav>
    );
}
