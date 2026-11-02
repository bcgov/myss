import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import {
  createMemoryRouter,
  MemoryRouter,
  Route,
  RouterProvider,
  Routes,
} from "react-router";

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
    }) as Session & { logout: ReturnType<typeof vi.fn> },
);

vi.mock("@/auth/useSession", () => ({
  useSession: () => session,
}));
// The token bridge, idle timer and error message catalogue are app-wide side
// effects with their own tests; here they only need to be inert.
vi.mock("@/auth/useApiAuth", () => ({ useApiAuth: () => undefined }));
vi.mock("@/auth/useIdleLogout", () => ({
  useIdleLogout: () => ({ warning: false, extendSession: vi.fn() }),
}));
vi.mock("@/hooks/useErrorMessages", () => ({
  useErrorMessageCatalogue: () => undefined,
}));
vi.mock("@/hooks/useFeatures", () => ({
  useFeatures: () => ({
    data: {
      Notifications: true,
      Messages: true,
      ServiceRequests: true,
      AccountInfo: true,
    },
    isError: false,
  }),
}));

import App from "./App";

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path={paths.home} element={<App />}>
          <Route index element={<p>Public home</p>} />
          <Route path={paths.dashboard} element={<p>Dashboard</p>} />
          <Route path={paths.messages} element={<p>Messages</p>} />
          <Route
            path={paths.serviceRequests}
            element={
              <label>
                Answer <input />
              </label>
            }
          />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe("App shell", () => {
  let initial = { width: 0, height: 0 };

  beforeAll(loadPageStyles);

  beforeEach(async () => {
    session.isAuthenticated = true;
    session.user = undefined;
    session.isMeLoading = false;
    session.hasProfile = true;
    session.logout.mockClear();
    initial = { width: window.innerWidth, height: window.innerHeight };
    await page.viewport(1280, 800);
  });

  afterEach(async () => {
    await page.viewport(initial.width, initial.height);
  });

  it.each([[paths.dashboard], [paths.messages]])(
    "puts Sign out in the header and drops the footer on %s",
    async (path) => {
      const screen = await renderAt(path);
      const header = screen.getByRole("banner");

      await header.getByRole("button", { name: "Sign out" }).click();
      expect(session.logout).toHaveBeenCalledOnce();
      await expect
        .element(screen.getByRole("contentinfo"))
        .not.toBeInTheDocument();
    },
  );

  it("keeps the ordinary page on public routes", async () => {
    const screen = await renderAt(paths.home);

    await expect.element(screen.getByText("Public home")).toBeVisible();
    await expect.element(screen.getByRole("contentinfo")).toBeInTheDocument();
    await expect
      .element(screen.getByRole("button", { name: "Sign out" }))
      .not.toBeInTheDocument();
  });

  it("keeps the ordinary page on a dashboard URL while signed out", async () => {
    // RequireAuth shows the sign-in chooser there; it needs the page padding
    // and has nothing to sign out of.
    session.isAuthenticated = false;
    const screen = await renderAt(paths.dashboard);

    await expect.element(screen.getByRole("contentinfo")).toBeInTheDocument();
    await expect
      .element(screen.getByRole("button", { name: "Sign out" }))
      .not.toBeInTheDocument();
  });

  it("shows the portal title in the header on a desktop", async () => {
    const screen = await renderAt(paths.home);

    await expect
      .element(screen.getByText("My Self Serve", { exact: true }))
      .toBeVisible();
  });

  describe("at phone width", () => {
    beforeEach(async () => {
      await page.viewport(320, 640);
    });

    it("drops the header title and keeps the page within the screen", async () => {
      const screen = await renderAt(paths.home);

      await expect.element(screen.getByRole("contentinfo")).toBeVisible();
      await expect
        .element(screen.getByText("My Self Serve", { exact: true }))
        .not.toBeVisible();
      const root = document.documentElement;
      expect(root.scrollWidth).toBeLessThanOrEqual(root.clientWidth);
    });

    it("puts a Menu button in the header of a dashboard page", async () => {
      const screen = await renderAt(paths.dashboard);
      const header = screen.getByRole("banner");

      await expect
        .element(header.getByRole("button", { name: "Menu" }))
        .toHaveAttribute("aria-expanded", "false");
      await expect
        .element(screen.getByRole("button", { name: "Sign out" }))
        .not.toBeInTheDocument();
      await expect.element(screen.getByText("Dashboard")).toBeVisible();
    });

    it("opens the menu in place of the page, and closes it again", async () => {
      const screen = await renderAt(paths.dashboard);
      await screen.getByRole("button", { name: "Menu" }).click();

      const button = screen.getByRole("button", { name: "Close Menu" });
      await expect.element(button).toHaveAttribute("aria-expanded", "true");
      const menuId = button.element().getAttribute("aria-controls");
      expect(document.getElementById(menuId ?? "")).not.toBeNull();
      const nav = screen.getByRole("navigation", { name: "My Self Serve" });
      for (const label of [
        "Home",
        "Notifications",
        "Messages",
        "Service Requests",
        "Account Info",
      ]) {
        await expect.element(nav.getByRole("link", { name: label })).toBeVisible();
      }
      await expect
        .element(nav.getByRole("link", { name: "Home" }))
        .toHaveAttribute("aria-current", "page");
      await expect
        .element(screen.getByRole("button", { name: "Sign out" }))
        .toBeVisible();
      await expect.element(screen.getByText("Dashboard")).not.toBeVisible();

      await button.click();

      await expect.element(screen.getByText("Dashboard")).toBeVisible();
      await expect.element(nav).not.toBeInTheDocument();
      await expect
        .element(screen.getByRole("button", { name: "Menu" }))
        .toHaveFocus();
    });

    it("closes on Escape and returns focus to the Menu button", async () => {
      const screen = await renderAt(paths.dashboard);
      await screen.getByRole("button", { name: "Menu" }).click();
      await screen.getByRole("link", { name: "Messages" }).element().focus();

      await userEvent.keyboard("{Escape}");

      await expect.element(screen.getByText("Dashboard")).toBeVisible();
      await expect
        .element(screen.getByRole("button", { name: "Menu" }))
        .toHaveFocus();
    });

    it("keeps a focused section's label light on the blue menu", async () => {
      const screen = await renderAt(paths.dashboard);
      await screen.getByRole("button", { name: "Menu" }).click();
      const link = screen.getByRole("link", { name: "Messages" }).element();

      const unfocused = getComputedStyle(link).color;
      (link as HTMLElement).focus();

      expect(getComputedStyle(link).color).toBe(unfocused);
    });

    it("opens the chosen section and moves focus to the page", async () => {
      const screen = await renderAt(paths.dashboard);
      await screen.getByRole("button", { name: "Menu" }).click();

      await screen.getByRole("link", { name: "Messages" }).click();

      await expect.element(screen.getByText("Messages")).toBeVisible();
      await expect
        .element(screen.getByRole("navigation"))
        .not.toBeInTheDocument();
      await expect.element(screen.getByRole("main")).toHaveFocus();
    });

    it("closes when the current section is chosen", async () => {
      const screen = await renderAt(paths.dashboard);
      await screen.getByRole("button", { name: "Menu" }).click();

      await screen.getByRole("link", { name: "Home" }).click();

      await expect.element(screen.getByText("Dashboard")).toBeVisible();
      await expect.element(screen.getByRole("main")).toHaveFocus();
    });

    it("keeps what was typed on the page while the menu is open", async () => {
      const screen = await renderAt(paths.serviceRequests);
      await screen.getByRole("textbox", { name: "Answer" }).fill("Draft answer");

      await screen.getByRole("button", { name: "Menu" }).click();
      await screen.getByRole("button", { name: "Close Menu" }).click();

      await expect
        .element(screen.getByRole("textbox", { name: "Answer" }))
        .toHaveValue("Draft answer");
    });

    it("closes when the route changes behind it", async () => {
      const router = createMemoryRouter(
        [
          {
            path: paths.home,
            element: <App />,
            children: [
              { path: paths.dashboard, element: <p>Dashboard</p> },
              { path: paths.messages, element: <p>Messages</p> },
            ],
          },
        ],
        { initialEntries: [paths.dashboard, paths.messages], initialIndex: 1 },
      );
      const screen = await render(<RouterProvider router={router} />);
      await screen.getByRole("button", { name: "Menu" }).click();

      await router.navigate(-1);

      await expect.element(screen.getByText("Dashboard")).toBeVisible();
      await expect
        .element(screen.getByRole("button", { name: "Menu" }))
        .toHaveAttribute("aria-expanded", "false");
    });

    it("signs out from the menu", async () => {
      const screen = await renderAt(paths.dashboard);
      await screen.getByRole("button", { name: "Menu" }).click();

      await screen.getByRole("button", { name: "Sign out" }).click();

      expect(session.logout).toHaveBeenCalledOnce();
    });

    it("closes the menu when the window widens past phone width", async () => {
      const screen = await renderAt(paths.dashboard);
      await screen.getByRole("button", { name: "Menu" }).click();
      await screen.getByRole("link", { name: "Messages" }).element().focus();

      await page.viewport(1280, 800);

      await expect.element(screen.getByText("Dashboard")).toBeVisible();
      await expect
        .element(screen.getByRole("banner").getByRole("button", { name: "Sign out" }))
        .toBeVisible();
      await expect.element(screen.getByRole("main")).toHaveFocus();

      await page.viewport(320, 640);
      await expect
        .element(screen.getByRole("button", { name: "Menu" }))
        .toHaveAttribute("aria-expanded", "false");
    });

    it("shows no Menu button on a public page", async () => {
      const screen = await renderAt(paths.home);

      await expect.element(screen.getByText("Public home")).toBeVisible();
      await expect
        .element(screen.getByRole("button", { name: "Menu" }))
        .not.toBeInTheDocument();
    });

    it.each([
      ["while the account is being checked", true],
      ["when the account lookup ends without an answer", false],
    ])("keeps Sign out in the header %s", async (_, isMeLoading) => {
      session.isMeLoading = isMeLoading;
      session.hasProfile = undefined;
      const screen = await renderAt(paths.dashboard);
      const header = screen.getByRole("banner");

      await expect
        .element(header.getByRole("button", { name: "Sign out" }))
        .toBeVisible();
      await expect
        .element(screen.getByRole("button", { name: "Menu" }))
        .not.toBeInTheDocument();
    });

    it("shows no Menu button to an IDIR session", async () => {
      session.user = {
        sub: "idir-user",
        name: "Staff",
        roles: ["WORKER"],
        idirUsername: "STAFFUSER",
      };
      const screen = await renderAt(paths.dashboard);

      await expect.element(screen.getByText("Dashboard")).toBeVisible();
      await expect
        .element(screen.getByRole("button", { name: "Menu" }))
        .not.toBeInTheDocument();
    });
  });
});
