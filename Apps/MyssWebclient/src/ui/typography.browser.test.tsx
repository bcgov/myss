import { afterAll, afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";

import { loadPageStyles } from "@/testing/pageStyles";

// A page rule that names a class, as a page title's own style does.
const ownSize = document.createElement("style");
ownSize.textContent = ".own-size { font-size: 40px; }";

function Sample() {
  return (
    <>
      <h1>Heading 1</h1>
      <h2>Heading 2</h2>
      <h3>Heading 3</h3>
      <h4>Heading 4</h4>
      <h5>Heading 5</h5>
      <h6>Heading 6</h6>
      <p>Body text</p>
      <h1 className="own-size">Own size</h1>
    </>
  );
}

function styleOf(element: Element) {
  const style = getComputedStyle(element);
  return {
    fontSize: parseFloat(style.fontSize),
    lineHeight: parseFloat(style.lineHeight),
    marginBottom: parseFloat(style.marginBottom),
  };
}

describe("typography", () => {
  let initial = { width: 0, height: 0 };

  beforeAll(async () => {
    await loadPageStyles();
    document.head.append(ownSize);
  });

  afterAll(() => {
    ownSize.remove();
  });

  beforeEach(() => {
    initial = { width: window.innerWidth, height: window.innerHeight };
  });

  afterEach(async () => {
    await page.viewport(initial.width, initial.height);
  });

  describe("on a phone", () => {
    beforeEach(async () => {
      await page.viewport(320, 640);
    });

    it.each([
      ["Heading 1", 32, 1.3, 8],
      ["Heading 2", 28, 1.3, 4],
      ["Heading 3", 24, 1.3, 4],
      ["Heading 4", 20, 1.4, 2],
      ["Heading 5", 18, 1.4, 2],
      ["Heading 6", 16, 1.4, 2],
    ])("sets %s to %ipx", async (name, size, lineHeight, marginBottom) => {
      const screen = await render(<Sample />);

      const style = styleOf(screen.getByText(name).element());
      expect(style.fontSize).toBe(size);
      expect(style.lineHeight).toBeCloseTo(size * lineHeight, 1);
      expect(style.marginBottom).toBe(marginBottom);
    });

    it("sets body text to 16 / 27 pixels", async () => {
      const screen = await render(<Sample />);

      const style = styleOf(screen.getByText("Body text").element());
      expect(style.fontSize).toBe(16);
      expect(style.lineHeight).toBeCloseTo(27, 1);
    });

    it("leaves a heading styled by a class at its own size", async () => {
      const screen = await render(<Sample />);

      expect(styleOf(screen.getByText("Own size").element()).fontSize).toBe(40);
    });
  });

  it("keeps the desktop sizes on a desktop", async () => {
    await page.viewport(1280, 800);
    const screen = await render(<Sample />);

    const sizes = [1, 2, 3, 4, 5, 6].map(
      (level) => styleOf(screen.getByText(`Heading ${level}`).element()).fontSize,
    );
    expect(sizes).toEqual([36, 32, 28, 24, 20, 18]);
    expect(styleOf(screen.getByText("Body text").element()).fontSize).toBe(14);
  });
});
