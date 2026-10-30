import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";

import { usePhoneLayout } from "./usePhoneLayout";

function Probe() {
  return <output>{usePhoneLayout() ? "phone" : "desktop"}</output>;
}

describe("usePhoneLayout", () => {
  let initial = { width: 0, height: 0 };

  beforeEach(() => {
    initial = { width: window.innerWidth, height: window.innerHeight };
  });

  afterEach(async () => {
    await page.viewport(initial.width, initial.height);
  });

  it.each([
    [320, "phone"],
    [767, "phone"],
    [768, "desktop"],
    [1280, "desktop"],
  ])("reports the %s-pixel width as %s", async (width, layout) => {
    await page.viewport(width, 800);
    const screen = await render(<Probe />);

    await expect.element(screen.getByRole("status")).toHaveTextContent(layout);
  });

  it("follows the window across the breakpoint while mounted", async () => {
    await page.viewport(1280, 800);
    const screen = await render(<Probe />);
    const probe = screen.getByRole("status");
    await expect.element(probe).toHaveTextContent("desktop");

    await page.viewport(375, 812);
    await expect.element(probe).toHaveTextContent("phone");

    await page.viewport(1280, 800);
    await expect.element(probe).toHaveTextContent("desktop");
  });
});
