import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import StatusPill from "./StatusPill";

describe("StatusPill", () => {
  it("shows the label for a status code", async () => {
    const screen = await render(<StatusPill status="RESUBMITTED" />);

    await expect.element(screen.getByText("Re-submitted")).toBeInTheDocument();
  });

  it("names the assignee of a file under review", async () => {
    const screen = await render(
      <StatusPill status="UNDER_REVIEW" assignee="JSMITH" />,
    );

    await expect
      .element(screen.getByText("Under Review - JSMITH"))
      .toBeInTheDocument();
  });

  it("names no assignee for any other status", async () => {
    const screen = await render(
      <StatusPill status="ESCALATED" assignee="JSMITH" />,
    );

    await expect
      .element(screen.getByText("Review", { exact: true }))
      .toBeInTheDocument();
  });

  it("falls back to the code for a status it has no label for", async () => {
    const screen = await render(<StatusPill status="ARCHIVED" />);

    await expect.element(screen.getByText("ARCHIVED")).toBeInTheDocument();
  });
});
