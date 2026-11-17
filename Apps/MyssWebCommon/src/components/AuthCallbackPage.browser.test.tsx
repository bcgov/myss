import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

// What react-oidc-context reports while (and after) it processes the callback.
const auth = vi.hoisted(() => ({
  isLoading: true,
  isAuthenticated: false,
  error: undefined as Error | undefined,
  user: undefined as { state?: unknown } | undefined,
}));

vi.mock("react-oidc-context", () => ({
  useAuth: () => auth,
}));

import AuthCallbackPage from "./AuthCallbackPage";

function renderCallback() {
  return render(
    <MemoryRouter initialEntries={["/auth/callback"]}>
      <Routes>
        <Route
          path="/auth/callback"
          element={<AuthCallbackPage fallbackPath="/landing" />}
        />
        <Route path="/landing" element={<p>Landing</p>} />
        <Route path="/started-here" element={<p>Started here</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("AuthCallbackPage", () => {
  beforeEach(() => {
    auth.isLoading = true;
    auth.isAuthenticated = false;
    auth.error = undefined;
    auth.user = undefined;
  });

  it("shows that sign-in is under way while the session resolves", async () => {
    const screen = await renderCallback();

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent("Signing you in…");
  });

  it("returns to the page the user started from", async () => {
    auth.isLoading = false;
    auth.isAuthenticated = true;
    auth.user = { state: { returnTo: "/started-here" } };

    const screen = await renderCallback();

    await expect.element(screen.getByText("Started here")).toBeInTheDocument();
  });

  it("goes to the app's fallback page when no return path was given", async () => {
    auth.isLoading = false;
    auth.isAuthenticated = true;
    auth.user = { state: undefined };

    const screen = await renderCallback();

    await expect.element(screen.getByText("Landing")).toBeInTheDocument();
  });

  // The page shows an alert for the error, but the same effect then
  // leaves for the fallback page, so a failed callback never strands the
  // user on it.
  it("leaves for the fallback page when sign-in failed", async () => {
    auth.isLoading = false;
    auth.error = new Error("invalid_grant");

    const screen = await renderCallback();

    await expect.element(screen.getByText("Landing")).toBeInTheDocument();
  });

  // MYSS-324: a silent sign-in (prompt=none) is refused when the identity
  // provider has no session; the library keeps the request's state on the
  // error, and the page that asked offers the chooser itself.
  it("returns to the page that asked when sign-in was refused", async () => {
    auth.isLoading = false;
    // The shape react-oidc-context reports: its own wrapper, with the
    // oidc-client-ts ErrorResponse (and the request's state) inside.
    auth.error = Object.assign(new Error("login_required"), {
      source: "signinCallback",
      innerError: Object.assign(new Error("login_required"), {
        error: "login_required",
        state: { returnTo: "/started-here" },
      }),
    });

    const screen = await renderCallback();

    await expect.element(screen.getByText("Started here")).toBeInTheDocument();
  });
});
