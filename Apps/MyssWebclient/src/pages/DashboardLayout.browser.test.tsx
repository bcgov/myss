import { beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import type { Session } from "@/auth/useSession";
import { paths } from "@/routes/paths";

const session = vi.hoisted(
  () =>
    ({
      user: undefined,
      isAuthenticated: true,
      isLoading: false,
      isMeLoading: false,
      hasProfile: true,
      profileFirstName: null,
      login: vi.fn(),
      logout: vi.fn(),
    }) as Session,
);

vi.mock("@/auth/useSession", () => ({
  useSession: () => session,
}));

import DashboardLayout from "./DashboardLayout";
import UnderDevelopmentPage from "./UnderDevelopmentPage";

// The layout is exercised with stand-in sections, except the placeholder
// page, which is the real one the router mounts for unbuilt features.
function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route element={<DashboardLayout />}>
          <Route path={paths.dashboard} element={<p>Home section</p>} />
          <Route
            path={paths.notifications}
            element={<UnderDevelopmentPage title="Notifications" />}
          />
          <Route
            path={paths.messages}
            element={<UnderDevelopmentPage title="Messages" />}
          />
          <Route
            path={paths.serviceRequests}
            element={<UnderDevelopmentPage title="Service Requests" />}
          />
          <Route
            path={paths.accountInfo}
            element={<UnderDevelopmentPage title="Account Info" />}
          />
        </Route>
        <Route path={paths.register} element={<p>Registration landing</p>} />
        <Route
          path={paths.workerApplications}
          element={<p>Worker landing</p>}
        />
      </Routes>
    </MemoryRouter>,
  );
}

describe("DashboardLayout", () => {
  beforeEach(() => {
    session.user = { sub: "user-1", name: "Alice", roles: [] };
    session.isMeLoading = false;
    session.hasProfile = true;
  });

  it("shows account checking while the profile lookup is pending", async () => {
    session.isMeLoading = true;
    const screen = await renderAt(paths.dashboard);

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent("Checking your MySS account…");
    await expect
      .element(screen.getByRole("navigation"))
      .not.toBeInTheDocument();
  });

  it("sends a Ministry worker to the worker view instead of registration", async () => {
    // A worker has no citizen profile; without the role check first they
    // would be bounced to the registration form.
    session.user = {
      sub: "idir-user",
      name: "Wanda",
      roles: ["WORKER"],
      idirUsername: "WWORKER",
    };
    session.hasProfile = false;
    const screen = await renderAt(paths.dashboard);

    await expect
      .element(screen.getByText("Worker landing"))
      .toBeInTheDocument();
  });

  it("waits for the roles before deciding where a signed-in user lands", async () => {
    session.user = { sub: "idir-user", roles: [], idirUsername: "WWORKER" };
    session.isMeLoading = true;
    session.hasProfile = undefined;
    const screen = await renderAt(paths.dashboard);

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent("Checking your MySS account…");
  });

  it("redirects an authenticated user without a profile to registration", async () => {
    session.hasProfile = false;
    const screen = await renderAt(paths.dashboard);

    await expect
      .element(screen.getByText("Registration landing"))
      .toBeInTheDocument();
  });

  it("guards every section, not only Home", async () => {
    session.hasProfile = false;
    const screen = await renderAt(paths.accountInfo);

    await expect
      .element(screen.getByText("Registration landing"))
      .toBeInTheDocument();
  });

  it("shows the menu beside Home with Home selected (AC1, AC2)", async () => {
    const screen = await renderAt(paths.dashboard);
    const nav = screen.getByRole("navigation", { name: "My Self Serve" });

    await expect.element(screen.getByText("Home section")).toBeVisible();
    for (const label of [
      "Home",
      "Notifications",
      "Messages",
      "Service Requests",
      "Account Info",
    ]) {
      await expect
        .element(nav.getByRole("link", { name: label }))
        .toBeVisible();
    }
    await expect
      .element(nav.getByRole("link", { name: "Home" }))
      .toHaveAttribute("aria-current", "page");
    await expect
      .element(nav.getByRole("link", { name: "Messages" }))
      .not.toHaveAttribute("aria-current");
  });

  it.each([
    ["Notifications"],
    ["Messages"],
    ["Service Requests"],
    ["Account Info"],
  ])(
    "opens the under-development page for %s, keeping the menu (AC3)",
    async (label) => {
      const screen = await renderAt(paths.dashboard);
      const nav = screen.getByRole("navigation", { name: "My Self Serve" });

      await nav.getByRole("link", { name: label }).click();

      await expect
        .element(screen.getByRole("heading", { level: 1, name: label }))
        .toBeVisible();
      await expect
        .element(screen.getByText("This feature is under development."))
        .toBeVisible();
      await expect
        .element(nav.getByRole("link", { name: label }))
        .toHaveAttribute("aria-current", "page");

      await nav.getByRole("link", { name: "Home" }).click();
      await expect.element(screen.getByText("Home section")).toBeVisible();
    },
  );
});
