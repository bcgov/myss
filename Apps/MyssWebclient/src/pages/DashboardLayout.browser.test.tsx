import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { MemoryRouter, Route, Routes } from "react-router";

import type { Session } from "@/auth/useSession";
import { paths } from "@/routes/paths";
import { loadPageStyles } from "@/testing/pageStyles";

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
      loginSilently: vi.fn(),
      logout: vi.fn(),
    }) as Session,
);

vi.mock("@/auth/useSession", () => ({
  useSession: () => session,
}));

const features = vi.hoisted(() => ({
  data: {
    Notifications: true,
    Messages: true,
    ServiceRequests: true,
    AccountInfo: true,
  } as Record<string, boolean>,
}));

vi.mock("@/hooks/useFeatures", () => ({
  useFeatures: () => ({ data: features.data, isError: false }),
}));

import DashboardLayout from "./DashboardLayout";
import UnderDevelopmentPage from "./UnderDevelopmentPage";

const copyright = `© ${new Date().getFullYear()} Government of British Columbia.`;

// The layout is exercised with stand-in sections, except the placeholder
// page, which is the real one the router mounts for unbuilt features. The
// flex `main` stands in for the app's, which fills the window.
function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          element={
            <main style={{ display: "flex", minHeight: "100vh" }}>
              <DashboardLayout />
            </main>
          }
        >
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
      </Routes>
    </MemoryRouter>,
  );
}

describe("DashboardLayout", () => {
  let initial = { width: 0, height: 0 };

  beforeAll(loadPageStyles);

  beforeEach(async () => {
    session.user = { sub: "user-1", name: "Alice", roles: [] };
    session.isMeLoading = false;
    session.hasProfile = true;
    vi.mocked(session.logout).mockClear();
    for (const name of Object.keys(features.data)) features.data[name] = true;
    initial = { width: window.innerWidth, height: window.innerHeight };
    await page.viewport(1280, 800);
  });

  afterEach(async () => {
    await page.viewport(initial.width, initial.height);
  });

  describe("at phone width", () => {
    beforeEach(async () => {
      await page.viewport(320, 640);
    });

    it("shows the section without the side menu, 16 pixels from the edge", async () => {
      const screen = await renderAt(paths.dashboard);
      const section = screen.getByText("Home section");

      await expect.element(section).toBeVisible();
      await expect
        .element(screen.getByRole("navigation"))
        .not.toBeInTheDocument();
      const text = section.element().getBoundingClientRect();
      expect(text.left).toBe(16);
      expect(text.right).toBe(document.documentElement.clientWidth - 16);
      const root = document.documentElement;
      expect(root.scrollWidth).toBeLessThanOrEqual(root.clientWidth);
    });

    it("ends the section with the copyright line across the screen", async () => {
      const screen = await renderAt(paths.dashboard);

      const section = screen.getByText("Home section").element().getBoundingClientRect();
      const footer = screen.getByText(copyright).element().parentElement!;
      const line = footer.getBoundingClientRect();
      expect(line.left).toBe(0);
      expect(line.right).toBe(document.documentElement.clientWidth);
      expect(line.top).toBeGreaterThanOrEqual(section.bottom);
      expect(getComputedStyle(footer).paddingLeft).toBe("22px");
      expect(getComputedStyle(footer).fontSize).toBe("14px");
    });
  });

  it("ends a short section with the copyright line at the bottom of the window, beside the menu", async () => {
    const screen = await renderAt(paths.dashboard);

    const nav = screen.getByRole("navigation").element().getBoundingClientRect();
    const line = screen.getByText(copyright).element().parentElement!.getBoundingClientRect();
    expect(line.left).toBe(nav.right);
    expect(line.right).toBe(document.documentElement.clientWidth);
    expect(line.bottom).toBe(window.innerHeight);
  });

  it.each([
    ["with the message for an IDIR session", { idirUsername: "STAFFUSER" }, false],
    ["while the account is being checked", {}, true],
  ])("shows the copyright line across the bottom of the window %s", async (_, user, isMeLoading) => {
    session.user = { sub: "user-1", name: "Alice", roles: [], ...user };
    session.isMeLoading = isMeLoading;
    const screen = await renderAt(paths.dashboard);

    await expect.element(screen.getByText(copyright)).toBeVisible();
    await expect
      .element(screen.getByText("Home section"))
      .not.toBeInTheDocument();
    const line = screen.getByText(copyright).element().parentElement!.getBoundingClientRect();
    expect(line.left).toBe(0);
    expect(line.right).toBe(document.documentElement.clientWidth);
    expect(line.bottom).toBe(window.innerHeight);
  });

  it("leaves a section whose feature flag is off out of the menu", async () => {
    features.data.Messages = false;
    const screen = await renderAt(paths.dashboard);

    await expect
      .element(screen.getByRole("link", { name: "Notifications" }))
      .toBeInTheDocument();
    await expect
      .element(screen.getByRole("link", { name: "Messages" }))
      .not.toBeInTheDocument();
  });

  it("tells an IDIR session this site is for citizens, instead of sending it to registration", async () => {
    session.user = {
      sub: "idir-user",
      name: "Staff",
      roles: ["WORKER"],
      idirUsername: "STAFFUSER",
    };
    session.hasProfile = false;
    const screen = await renderAt(paths.dashboard);

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent(/My Self Serve Administration/);
    await expect
      .element(screen.getByText("Registration landing"))
      .not.toBeInTheDocument();
    await expect
      .element(screen.getByText("Home section"))
      .not.toBeInTheDocument();

    await screen.getByRole("button", { name: "Sign out" }).click();
    expect(session.logout).toHaveBeenCalledOnce();
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

  it("waits for the account lookup before deciding where a signed-in user lands", async () => {
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
