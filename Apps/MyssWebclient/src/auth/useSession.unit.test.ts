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
});
