import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import AnnouncementBanner from "./AnnouncementBanner";

describe("AnnouncementBanner", () => {
  it("shows its message in a labelled announcement region", async () => {
    const screen = await render(
      <AnnouncementBanner>You will be signed out soon.</AnnouncementBanner>,
    );

    await expect
      .element(screen.getByRole("complementary", { name: "Announcement" }))
      .toHaveTextContent("You will be signed out soon.");
  });
});
