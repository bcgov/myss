import { describe, expect, it, vi } from "vitest";
import type { AuthContextProps } from "react-oidc-context";

import { buildSession } from "./useSession";

function fakeAuth(overrides: Partial<AuthContextProps> = {}): AuthContextProps {
  return {
    isAuthenticated: false,
    isLoading: false,
    user: null,
    signinRedirect: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  } as unknown as AuthContextProps;
}

describe("buildSession", () => {
  it("signs in through IDIR, carrying where to return to", () => {
    const auth = fakeAuth();
    buildSession(auth, vi.fn()).login("/form-management");
    expect(auth.signinRedirect).toHaveBeenCalledWith({
      extraQueryParams: { kc_idp_hint: "azureidir" },
      state: { returnTo: "/form-management" },
    });
  });

  it("sends no state when there is nowhere to return to", () => {
    const auth = fakeAuth();
    buildSession(auth, vi.fn()).login();
    expect(auth.signinRedirect).toHaveBeenCalledWith({
      extraQueryParams: { kc_idp_hint: "azureidir" },
    });
  });

  it("takes the user from the id token", () => {
    const auth = fakeAuth({
      isAuthenticated: true,
      user: {
        profile: { sub: "abc@azureidir", idir_username: "JDOE" },
      } as unknown as AuthContextProps["user"],
    });
    const session = buildSession(auth, vi.fn());
    expect(session.isAuthenticated).toBe(true);
    expect(session.user?.idirUsername).toBe("JDOE");
  });

  it("has no user while signed out", () => {
    expect(buildSession(fakeAuth(), vi.fn()).user).toBeUndefined();
  });
});
