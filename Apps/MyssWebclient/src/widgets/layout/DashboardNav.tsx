import type { ReactNode } from "react";
import { NavLink } from "react-router";

import {
  dashboardSections,
  type DashboardIcon,
} from "@/routes/dashboardSections";
import styles from "./DashboardNav.module.css";

// The dashboard's left-hand menu (MYSS-194). NavLink marks the current
// section with aria-current="page", which is also what the selected style
// keys on, so the visual and the accessible state cannot drift apart.

// Glyphs from Material Icons (Apache License 2.0), drawn with currentColor so
// they follow the link's text colour. Decorative: the label names the link.
const ICON_PATHS: Readonly<Record<DashboardIcon, string>> = {
  home: "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z",
  bell: "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.89 2 2 2zm6-6v-5c0-3.07-1.64-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.63 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2z",
  mail: "M20 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 4l-8 5-8-5V6l8 5 8-5v2z",
  document:
    "M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z",
  person:
    "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z",
};

function NavIcon({ icon }: { icon: DashboardIcon }): ReactNode {
  return (
    <svg
      className={styles.icon}
      viewBox="0 0 24 24"
      aria-hidden="true"
      focusable="false"
    >
      <path d={ICON_PATHS[icon]} fill="currentColor" />
    </svg>
  );
}

export default function DashboardNav() {
  return (
    <nav aria-label="My Self Serve" className={styles.nav}>
      <ul className={styles.list}>
        {dashboardSections.map((section) => (
          <li key={section.path}>
            {/* `end`: Home is /dashboard and must not stay selected on deeper paths. */}
            <NavLink to={section.path} end className={styles.link}>
              <NavIcon icon={section.icon} />
              <span>{section.label}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  );
}
