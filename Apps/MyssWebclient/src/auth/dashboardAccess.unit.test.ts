import { describe, expect, it } from "vitest";

import { getDashboardAccess } from "./dashboardAccess";
import type { CurrentUser } from "./currentUser";

const citizen: CurrentUser = { sub: "citizen-1", roles: [] };
const staff: CurrentUser = {
  sub: "idir-1",
  roles: ["WORKER"],
  idirUsername: "STAFFUSER",
};

describe("getDashboardAccess", () => {
  it.each([
    ["a citizen with a profile", citizen, true, "ready"],
    ["a citizen without a profile", citizen, false, "unregistered"],
    ["a citizen whose profile is unknown", citizen, undefined, "checking"],
    ["an IDIR session without a profile", staff, false, "staff"],
    ["an IDIR session with a profile", staff, true, "staff"],
  ] as const)("places %s", (_, user, hasProfile, expected) => {
    expect(getDashboardAccess({ user, isMeLoading: false, hasProfile })).toBe(
      expected,
    );
  });

  it.each([
    ["a citizen", citizen],
    ["an IDIR session", staff],
  ])("waits for the account lookup for %s", (_, user) => {
    expect(
      getDashboardAccess({ user, isMeLoading: true, hasProfile: false }),
    ).toBe("checking");
  });
});
