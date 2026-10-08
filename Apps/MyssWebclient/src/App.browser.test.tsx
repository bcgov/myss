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

import App from "./App";

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path={paths.home} element={<App />}>
          <Route index element={<p>Public home</p>} />
          <Route path={paths.dashboard} element={<p>Dashboard</p>} />
          <Route path={paths.messages} element={<p>Messages</p>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  );
}

describe("App shell", () => {
  beforeEach(() => {
    session.isAuthenticated = true;
    session.logout.mockClear();
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
});
