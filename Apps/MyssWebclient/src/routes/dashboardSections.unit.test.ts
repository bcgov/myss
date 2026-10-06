import { describe, expect, it } from "vitest";

import { dashboardSections, isDashboardPath } from "./dashboardSections";
import { paths } from "./paths";

describe("dashboardSections", () => {
  it("lists the MYSS-194 menu in order, Home first", () => {
    expect(dashboardSections.map((section) => section.label)).toEqual([
      "Home",
      "Notifications",
      "Messages",
      "Service Requests",
      "Account Info",
    ]);
    expect(dashboardSections[0].path).toBe(paths.dashboard);
  });

  it("gives every section its own path", () => {
    const sectionPaths = dashboardSections.map((section) => section.path);
    expect(new Set(sectionPaths).size).toBe(sectionPaths.length);
  });
});

describe("isDashboardPath", () => {
  it("matches every section, with or without a trailing slash", () => {
    for (const section of dashboardSections) {
      expect(isDashboardPath(section.path)).toBe(true);
      expect(isDashboardPath(`${section.path}/`)).toBe(true);
    }
  });

  it("does not match public pages or pages below a section", () => {
    expect(isDashboardPath(paths.home)).toBe(false);
    expect(isDashboardPath(paths.eligibilityEstimator)).toBe(false);
    expect(isDashboardPath(paths.register)).toBe(false);
    expect(isDashboardPath("/applications/abc")).toBe(false);
    expect(isDashboardPath("/messages/123")).toBe(false);
  });
});
