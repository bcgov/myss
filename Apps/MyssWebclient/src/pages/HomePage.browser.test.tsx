import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { MemoryRouter } from "react-router";

import type { Session } from "@/auth/useSession";
import { loadPageStyles } from "@/testing/pageStyles";
import "@/App.css";

vi.mock("@/auth/useSession", () => ({
  useSession: () =>
    ({
      user: undefined,
      isAuthenticated: false,
      isLoading: false,
      login: vi.fn(),
      logout: vi.fn(),
    }) as Partial<Session>,
}));
vi.mock("@/hooks/useFeatures", () => ({
  useFeature: () => true,
}));
// The videos are embedded players from another site.
vi.mock("@/widgets/home/VideoSection", () => ({
  default: () => <p>Videos</p>,
}));

import HomePage from "./HomePage";

function renderHome() {
  return render(
    <MemoryRouter>
      <main>
        <HomePage />
      </main>
    </MemoryRouter>,
  );
}

describe("HomePage layout", () => {
  let initial = { width: 0, height: 0 };

  beforeAll(loadPageStyles);

  beforeEach(() => {
    initial = { width: window.innerWidth, height: window.innerHeight };
  });

  afterEach(async () => {
    await page.viewport(initial.width, initial.height);
  });

  it("starts every section 16 pixels from the edge on a phone", async () => {
    await page.viewport(320, 640);
    const screen = await renderHome();
    await expect
      .element(screen.getByRole("heading", { name: "About MySS" }))
      .toBeVisible();

    const callouts = [...document.querySelectorAll(".bcds-Callout")];
    const headings = [...document.querySelectorAll("main h1, main h2")].filter(
      (heading) => !heading.closest(".bcds-Callout"),
    );
    expect(callouts).toHaveLength(2);
    expect(headings.length).toBeGreaterThanOrEqual(4);
    // Where a heading's text starts, so padding inside its box counts.
    for (const heading of headings) {
      const text = document.createRange();
      text.selectNodeContents(heading);
      expect(text.getBoundingClientRect().left, heading.textContent ?? "").toBe(16);
    }
    for (const callout of callouts) {
      expect(callout.getBoundingClientRect().left).toBe(16);
    }
    const root = document.documentElement;
    expect(root.scrollWidth).toBeLessThanOrEqual(root.clientWidth);
  });
});
