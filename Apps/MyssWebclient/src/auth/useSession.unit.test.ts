import { describe, it, expect, vi } from "vitest";

import { buildSession } from "./useSession";

function fakeAuth(overrides: Record<string, unknown> = {}) {
  return {
    isAuthenticated: false,
    isLoading: false,
    user: undefined,
    signinRedirect: vi.fn(),
    ...overrides,
  };
}

describe("buildSession", () => {
  it("passes through loading and authenticated flags", () => {
    const s = buildSession(
      fakeAuth({ isAuthenticated: true, isLoading: false }) as never,
      vi.fn(),
    );
    expect(s.isAuthenticated).toBe(true);
    expect(s.isLoading).toBe(false);
  });

  it("normalizes the user profile when present", () => {
    const s = buildSession(
      fakeAuth({
        isAuthenticated: true,
        user: { profile: { sub: "u1", name: "Alice" } },
      }) as never,
      vi.fn(),
    );
    expect(s.user?.sub).toBe("u1");
    expect(s.user?.name).toBe("Alice");
  });

  it("returns undefined user when not signed in", () => {
    const s = buildSession(fakeAuth() as never, vi.fn());
    expect(s.user).toBeUndefined();
  });

  it("login redirects with the correct kc_idp_hint", () => {
    const auth = fakeAuth();
    const s = buildSession(auth as never, vi.fn());
    s.login("bceid");
    expect(auth.signinRedirect).toHaveBeenCalledWith({
      extraQueryParams: { kc_idp_hint: "bceidbasic" },
    });
  });

  it("logout delegates to the injected logout function", () => {
    const logout = vi.fn();
    const s = buildSession(fakeAuth() as never, logout);
    s.logout();
    expect(logout).toHaveBeenCalledOnce();
  });

  // Roles come from GET /v1/auth/me — the server-computed effective roles
  // (RoleCalculator, ADR-0007) — never from token claims: the browser cannot
  // see the derive switch, nor MySS account state once the APPLICANT/CLIENT
  // split lands.
  describe("roles from the /auth/me response", () => {
    const authed = () =>
      fakeAuth({
        isAuthenticated: true,
        user: { profile: { sub: "u1", client_roles: ["WORKER"] } },
      }) as never;

    it("uses the me payload's roles", () => {
      const s = buildSession(authed(), vi.fn(), {
        isAuthenticated: true,
        subject: "u1",
        roles: ["CLIENT"],
      });
      expect(s.user?.roles).toEqual(["CLIENT"]);
    });

    it("ignores role-shaped token claims entirely", () => {
      const s = buildSession(authed(), vi.fn(), {
        isAuthenticated: true,
        subject: "u1",
        roles: [],
      });
      expect(s.user?.roles).toEqual([]);
    });

    it("falls back to no roles while the me response is absent", () => {
      const s = buildSession(authed(), vi.fn());
      expect(s.user?.roles).toEqual([]);
    });

    it("reports loading while authenticated and the me query is pending", () => {
      const s = buildSession(authed(), vi.fn(), undefined, true);
      expect(s.isLoading).toBe(true);
    });

    it("does not report loading for a signed-out visitor", () => {
      const s = buildSession(fakeAuth() as never, vi.fn(), undefined, true);
      expect(s.isLoading).toBe(false);
    });
  });
});
