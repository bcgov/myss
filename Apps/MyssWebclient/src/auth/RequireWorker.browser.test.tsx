import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import type { Session } from "@/auth/useSession";
import { paths } from "@/routes/paths";

const session = vi.hoisted(
  () =>
    ({
      user: undefined,
      isAuthenticated: false,
      isLoading: false,
      isMeLoading: false,
      login: vi.fn(),
      logout: vi.fn(),
    }) as Session & { login: ReturnType<typeof vi.fn> },
);

vi.mock("@/auth/useSession", () => ({
  useSession: () => session,
}));

import RequireWorker from "./RequireWorker";

function renderGuard(initialEntry: string = paths.workerApplications) {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path={paths.home} element={<p>Regular landing</p>} />
        <Route
          path="/worker/*"
          element={
            <RequireWorker>
              <p>Worker landing</p>
            </RequireWorker>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

describe("RequireWorker", () => {
  beforeEach(() => {
    session.user = undefined;
    session.isAuthenticated = false;
    session.isLoading = false;
    session.isMeLoading = false;
    session.login.mockClear();
  });

  it("shows a loading status while the session resolves", async () => {
    session.isLoading = true;

    const screen = await renderGuard();

    await expect.element(screen.getByText("Loading…")).toBeInTheDocument();
    expect(session.login).not.toHaveBeenCalled();
  });

  it("starts IDIR login once and preserves the exact destination", async () => {
    const destination = `${paths.workerApplications}?sort=oldest#top`;
    const screen = await renderGuard(destination);

    await expect
      .element(screen.getByText("Redirecting to IDIR sign in…"))
      .toBeInTheDocument();
    await vi.waitFor(() => {
      expect(session.login).toHaveBeenCalledOnce();
    });
    expect(session.login).toHaveBeenCalledWith("idir", destination);
  });

  it("keeps waiting while the roles are still being fetched", async () => {
    // Roles come from /auth/me after authentication; deciding before they
    // arrive would bounce a real worker to home.
    session.isAuthenticated = true;
    session.isMeLoading = true;
    session.user = { sub: "idir-user", roles: [], idirUsername: "WWORKER" };

    const screen = await renderGuard();

    await expect.element(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("redirects an authenticated citizen to home", async () => {
    session.isAuthenticated = true;
    session.user = { sub: "bceid-user", roles: ["CLIENT"] };

    const screen = await renderGuard();

    await expect
      .element(screen.getByText("Regular landing"))
      .toBeInTheDocument();
  });

  it("redirects an IDIR user without the worker role to home", async () => {
    session.isAuthenticated = true;
    session.user = { sub: "idir-user", roles: [], idirUsername: "IDIRUSER" };

    const screen = await renderGuard();

    await expect
      .element(screen.getByText("Regular landing"))
      .toBeInTheDocument();
  });

  it("renders children for an IDIR user with the worker role", async () => {
    session.isAuthenticated = true;
    session.user = {
      sub: "idir-user",
      roles: ["WORKER"],
      idirUsername: "WWORKER",
    };

    const screen = await renderGuard();

    await expect
      .element(screen.getByText("Worker landing"))
      .toBeInTheDocument();
  });
});
