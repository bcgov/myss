import { describe, expect, it } from "vitest";
import { render } from "vitest-browser-react";

import MessageText from "./MessageText";

describe("MessageText", () => {
  it("renders paragraphs, line breaks, bold and a list", async () => {
    const screen = await render(
      <div data-testid="message">
        <MessageText text={"First line.\nSecond **bold** line.\n\nBring:\n- your card\n- your number"} />
      </div>,
    );
    const message = screen.getByTestId("message").element();

    expect(message.querySelectorAll("p")).toHaveLength(2);
    expect(message.querySelectorAll("p br")).toHaveLength(1);
    expect(message.querySelector("strong")?.textContent).toBe("bold");
    expect(
      Array.from(message.querySelectorAll("ul > li"), (item) => item.textContent),
    ).toEqual(["your card", "your number"]);
  });

  it("opens a link in a new tab and says so to screen readers", async () => {
    const screen = await render(
      <MessageText text="See [the program](https://www2.gov.bc.ca)." />,
    );
    const link = screen.getByRole("link", {
      name: "the program (opens in a new tab)",
    });

    await expect.element(link).toHaveAttribute("href", "https://www2.gov.bc.ca");
    await expect.element(link).toHaveAttribute("target", "_blank");
    await expect.element(link).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("opens email and phone links in place, without a new-tab note", async () => {
    const screen = await render(
      <MessageText text="[Email us](mailto:buspass@example.com) or [call us](tel:18005551234)." />,
    );

    for (const [name, href] of [
      ["Email us", "mailto:buspass@example.com"],
      ["call us", "tel:18005551234"],
    ]) {
      const link = screen.getByRole("link", { name, exact: true });
      await expect.element(link).toHaveAttribute("href", href);
      await expect.element(link).not.toHaveAttribute("target");
    }
  });

  it("shows HTML in a message as text", async () => {
    const screen = await render(
      <div data-testid="message">
        <MessageText text="<img src=x onerror=alert(1)> and <b>this</b>" />
      </div>,
    );
    const message = screen.getByTestId("message").element();

    expect(message.querySelector("img")).toBeNull();
    expect(message.querySelector("b")).toBeNull();
    expect(message.textContent).toBe("<img src=x onerror=alert(1)> and <b>this</b>");
  });

  it("puts a value in place of its placeholder, as text", async () => {
    const screen = await render(
      <div data-testid="message">
        <MessageText
          text="Your number is {referenceNumber}."
          values={{ referenceNumber: "**1-0123**[x](https://example.com)" }}
        />
      </div>,
    );
    const message = screen.getByTestId("message").element();

    expect(message.textContent).toBe(
      "Your number is **1-0123**[x](https://example.com).",
    );
    expect(message.querySelector("strong")).toBeNull();
    expect(message.querySelector("a")).toBeNull();
  });

  it("leaves a placeholder without a value as typed", async () => {
    const screen = await render(
      <div data-testid="message">
        <MessageText text="Your number is {referenceNumber}." />
      </div>,
    );

    expect(screen.getByTestId("message").element().textContent).toBe(
      "Your number is {referenceNumber}.",
    );
  });
});
