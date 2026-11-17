import { afterEach, describe, expect, it } from "vitest";

import { buildOidcConfig } from "./oidc";

// The sign-in callback against the browser's real History API.

const startUrl = `${location.pathname}${location.search}`;
const startState: unknown = history.state;

afterEach(() => {
  history.replaceState(startState, "", startUrl);
});

describe("buildOidcConfig: onSigninCallback", () => {
  it("strips the code and state from the URL and keeps the history state", () => {
    // React Router keeps its position in the history state; Back relies on it.
    const routerState = { usr: null, key: "abc123", idx: 4 };
    history.replaceState(
      routerState,
      "",
      "/auth/callback?code=c0de&state=s7a7e",
    );

    buildOidcConfig({
      authority: "https://dev.loginproxy.gov.bc.ca/auth/realms/standard",
      clientId: "sdpr-my-ss-6498",
      origin: location.origin,
    }).onSigninCallback();

    expect(location.pathname).toBe("/auth/callback");
    expect(location.search).toBe("");
    expect(history.state).toEqual(routerState);
  });
});
