import { matchPath } from "react-router";

import { paths } from "@/routes/paths";

// The signed-in dashboard's sections, in menu order (MYSS-194). One list
// drives the side navigation and tells the app shell which routes get the
// dashboard chrome (header Sign out, no footer). Connecting a feature page
// is a change to its route in router.tsx only; the menu does not move.
//
// This is the menu for a client with no linked case (or a closed one). A
// linked client's menu will differ; when that lands, choose the list here
// rather than branching inside the navigation widget.

export type DashboardIcon = "home" | "bell" | "mail" | "document" | "person";

export interface DashboardSection {
  path: string;
  label: string;
  icon: DashboardIcon;
}

export const dashboardSections: readonly DashboardSection[] = [
  { path: paths.dashboard, label: "Home", icon: "home" },
  { path: paths.notifications, label: "Notifications", icon: "bell" },
  { path: paths.messages, label: "Messages", icon: "mail" },
  { path: paths.serviceRequests, label: "Service Requests", icon: "document" },
  { path: paths.accountInfo, label: "Account Info", icon: "person" },
];

/** True when `pathname` is one of the dashboard sections (trailing slash allowed). */
export function isDashboardPath(pathname: string): boolean {
  return dashboardSections.some(
    (section) =>
      matchPath({ path: section.path, end: true }, pathname) !== null,
  );
}
