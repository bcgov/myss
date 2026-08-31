import { describe, it, expect } from "vitest";

import { normalizeUser } from "./currentUser";

describe("normalizeUser", () => {
  it("maps core identity claims", () => {
    const user = normalizeUser({
      sub: "abc-123",
      name: "Alice Applicant",
      email: "alice@example.com",
    });
    expect(user.sub).toBe("abc-123");
    expect(user.name).toBe("Alice Applicant");
    expect(user.email).toBe("alice@example.com");
  });

  it("falls back to display_name when name is absent", () => {
    expect(normalizeUser({ sub: "x", display_name: "Bob B" }).name).toBe(
      "Bob B",
    );
  });

  // BC Services Card sends no name and no display_name — it sends the given
  // names under the PLURAL spelling, plus family_name. Observed on a real
  // BCSC token 2026-08-13.
  it("builds a name from given_names + family_name (BC Services Card)", () => {
    expect(
      normalizeUser({
        sub: "x",
        given_names: "GATEWAY Carlos",
        family_name: "ELEVEN",
        preferred_username: "r66eF0hnb8SsRs4UkuXxy3nEcQDR7FtDlqtGzd+kPkQ=",
      }).name,
    ).toBe("GATEWAY Carlos ELEVEN");
  });

  it("builds a name from the singular given_name spelling (IDIR / BCeID)", () => {
    expect(
      normalizeUser({
        sub: "x",
        given_name: "Alice",
        family_name: "Applicant",
      }).name,
    ).toBe("Alice Applicant");
  });

  it("uses whichever half of the name is present", () => {
    expect(normalizeUser({ sub: "x", family_name: "ELEVEN" }).name).toBe(
      "ELEVEN",
    );
    expect(normalizeUser({ sub: "x", given_names: "Carlos" }).name).toBe(
      "Carlos",
    );
  });

  // Greeting a citizen with an opaque directed identifier is worse than
  // greeting them with no name at all, so preferred_username is not a name
  // source. AccountPanel renders a bare "Welcome back" when name is absent.
  it("never uses preferred_username as a display name", () => {
    expect(
      normalizeUser({
        sub: "x",
        preferred_username: "r66eF0hnb8SsRs4UkuXxy3nEcQDR7FtDlqtGzd+kPkQ=",
      }).name,
    ).toBeUndefined();
  });

  it("surfaces bceid guid and idir username under either claim spelling", () => {
    expect(
      normalizeUser({ sub: "x", bceid_user_guid: "guid-1" }).bceidGuid,
    ).toBe("guid-1");
    expect(normalizeUser({ sub: "x", bceid_guid: "guid-2" }).bceidGuid).toBe(
      "guid-2",
    );
    expect(
      normalizeUser({ sub: "x", idir_username: "AJONES" }).idirUsername,
    ).toBe("AJONES");
  });

  // Roles are deliberately NOT normalized here: they come server-computed
  // from GET /v1/auth/me (see useSession + ADR-0007), never from token claims.
  it("exposes no roles field", () => {
    expect(
      "roles" in normalizeUser({ sub: "x", client_roles: ["CLIENT"] }),
    ).toBe(false);
  });
});
