import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import Callout from "./Callout";

describe("Callout", () => {
  it("shows the title as a level 2 heading by default", async () => {
    const screen = await render(<Callout title="Need a bus pass?" />);

    await expect
      .element(
        screen.getByRole("heading", { level: 2, name: "Need a bus pass?" }),
      )
      .toBeInTheDocument();
  });

  it("uses the heading level it is given", async () => {
    const screen = await render(
      <Callout title="Payment information" headingLevel={3} />,
    );

    await expect
      .element(
        screen.getByRole("heading", { level: 3, name: "Payment information" }),
      )
      .toBeInTheDocument();
  });

  it("renders nested content below the title", async () => {
    const screen = await render(
      <Callout title="Payment information">
        <p>The upcoming cheque issue date is July 29.</p>
        <a href="/buspass">Apply for a bus pass</a>
      </Callout>,
    );

    await expect
      .element(screen.getByText("The upcoming cheque issue date is July 29."))
      .toBeInTheDocument();
    await expect
      .element(screen.getByRole("link", { name: "Apply for a bus pass" }))
      .toHaveAttribute("href", "/buspass");
  });

  it("renders no heading when there is no title", async () => {
    const screen = await render(
      <Callout>
        <p>Content only.</p>
      </Callout>,
    );

    await expect.element(screen.getByText("Content only.")).toBeInTheDocument();
    expect(screen.container.querySelector("h1, h2, h3, h4, h5, h6")).toBeNull();
  });

  it("passes the variant through to the design system callout", async () => {
    const screen = await render(<Callout title="Note" variant="lightBlue" />);

    expect(
      screen.container.querySelector(".bcds-Callout.lightBlue"),
    ).not.toBeNull();
  });

  it("draws an 8px primary blue bar on the grey variant", async () => {
    const screen = await render(<Callout title="Note" variant="Grey" />);

    const box = screen.container.querySelector(".bcds-Callout.Grey");
    const style = getComputedStyle(box as Element);
    expect(style.borderLeftWidth).toBe("8px");
    // --theme-primary-blue, #013366
    expect(style.borderLeftColor).toBe("rgb(1, 51, 102)");
  });

  it("sets the text inside at 16px", async () => {
    const screen = await render(
      <Callout>
        <p>Body text.</p>
      </Callout>,
    );

    const text = screen.getByText("Body text.").element();
    expect(getComputedStyle(text).fontSize).toBe("16px");
  });

  it("draws text and links in the secondary colour on the light grey variant", async () => {
    const screen = await render(
      <Callout variant="lightGrey" title="Privacy">
        <p>
          Visit <a href="/buspass">the program page</a>.
        </p>
      </Callout>,
    );

    // --typography-color-secondary, #474543
    const secondary = "rgb(71, 69, 67)";
    const link = getComputedStyle(
      screen.getByRole("link", { name: "the program page" }).element(),
    );
    expect(link.color).toBe(secondary);
    expect(link.textDecorationLine).toBe("underline");
    expect(
      getComputedStyle(screen.getByText("Visit", { exact: false }).element())
        .color,
    ).toBe(secondary);
    // The heading keeps the primary colour, #2D2D2D.
    expect(
      getComputedStyle(
        screen.getByRole("heading", { name: "Privacy" }).element(),
      ).color,
    ).toBe("rgb(45, 45, 45)");
  });

  it("outlines the other three sides in the light border colour", async () => {
    const screen = await render(<Callout title="Note" variant="lightGrey" />);

    const style = getComputedStyle(
      screen.container.querySelector(".bcds-Callout") as Element,
    );
    // --surface-color-border-default, #D8D8D8
    for (const side of ["Top", "Right", "Bottom"] as const) {
      expect(style[`border${side}Width`], side).toBe("1px");
      expect(style[`border${side}Style`], side).toBe("solid");
      expect(style[`border${side}Color`], side).toBe("rgb(216, 216, 216)");
    }
    expect(style.borderLeftWidth).toBe("8px");
  });
});
