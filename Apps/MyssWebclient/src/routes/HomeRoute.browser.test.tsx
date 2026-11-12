import { afterEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";

import { LEGACY_MYSS_URL } from "@/constants";

// The home page feature toggle (MYSS-329): on, `/` shows the home page; off,
// the visitor goes to legacy MySelfServe; nothing shows while the flags load.

const flags = vi.hoisted(() => ({ homePage: undefined as boolean | undefined }));
vi.mock("@/hooks/useFeatures", () => ({
  useFeature: () => flags.homePage,
}));
const leaveSite = vi.hoisted(() => vi.fn());
vi.mock("@/routes/leaveSite", () => ({ leaveSite }));

import HomeRoute from "./HomeRoute";

function renderHome() {
  return render(
    <HomeRoute>
      <h1>Home</h1>
    </HomeRoute>,
  );
}

describe("HomeRoute", () => {
  afterEach(() => {
    leaveSite.mockClear();
    flags.homePage = undefined;
  });

  it("shows the home page while the flag is on", async () => {
    flags.homePage = true;
    const screen = await renderHome();

    await expect.element(screen.getByRole("heading", { name: "Home" })).toBeVisible();
    expect(leaveSite).not.toHaveBeenCalled();
  });

  it("sends the visitor to legacy MySelfServe while the flag is off", async () => {
    flags.homePage = false;
    const screen = await renderHome();

    await vi.waitFor(() => expect(leaveSite).toHaveBeenCalledWith(LEGACY_MYSS_URL));
    expect(leaveSite).toHaveBeenCalledOnce();
    await expect.element(screen.getByRole("heading", { name: "Home" })).not.toBeInTheDocument();
  });

  it("shows nothing and goes nowhere while the flags load", async () => {
    const screen = await renderHome();

    await expect.element(screen.getByRole("heading", { name: "Home" })).not.toBeInTheDocument();
    expect(leaveSite).not.toHaveBeenCalled();
  });
});
