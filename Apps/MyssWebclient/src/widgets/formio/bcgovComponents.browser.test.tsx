import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it } from "vitest";

import "@formio/js/dist/formio.form.min.css";
import { registerBcgovComponents } from "./bcgovComponents";

// The accordion body is HTML that reaches the browser from the form spec, so a
// content designer with edit access could put a payload in it. It renders
// through dangerouslySetInnerHTML, guarded only by the Utils.sanitize call in
// bcgovComponents. These tests are that guard's only coverage.
//
// The payloads set a flag rather than calling alert(): an inline handler that
// survived sanitization would fire and set it, which is a behavioural
// assertion rather than an inspection of the markup. A `<script>` tag is
// deliberately NOT used — browsers never execute scripts inserted via
// innerHTML, so it would pass with the sanitizer removed.

declare global {
  interface Window {
    __xssFired?: boolean;
  }
}

function accordionSpec(body: string): FormType {
  return {
    display: "form",
    components: [
      {
        type: "bcgovAccordion",
        key: "help",
        accordionLabel: "Open me",
        accordionBody: body,
        input: false,
      },
    ],
  } as unknown as FormType;
}

registerBcgovComponents();

afterEach(() => {
  delete window.__xssFired;
});

describe("bcgovAccordion body sanitization", () => {
  it("renders safe markup in the body", async () => {
    const screen = await render(
      <Form src={accordionSpec("<p>Plain guidance text.</p>")} />,
    );

    await screen.getByRole("button", { name: "Open me" }).click();
    await expect
      .element(screen.getByText("Plain guidance text."))
      .toBeVisible();
  });

  it("strips an inline event handler that would otherwise run", async () => {
    const screen = await render(
      <Form
        src={accordionSpec(
          '<p>Still here.</p><img src="x" onerror="window.__xssFired = true">',
        )}
      />,
    );

    await screen.getByRole("button", { name: "Open me" }).click();
    // The surrounding markup renders, so the body really did reach the DOM —
    // without this the absence of the handler would prove nothing.
    await expect.element(screen.getByText("Still here.")).toBeVisible();

    const image = document.querySelector("img[src='x']");
    expect(image, "the img was removed entirely, so nothing was proven").not
      .toBeNull();
    expect(image?.getAttribute("onerror")).toBeNull();
    expect(window.__xssFired).toBeUndefined();
  });

  it("strips a javascript: link target", async () => {
    const screen = await render(
      <Form
        src={accordionSpec(
          '<a href="javascript:window.__xssFired = true">Read more</a>',
        )}
      />,
    );

    await screen.getByRole("button", { name: "Open me" }).click();

    const link = document.querySelector("a");
    expect(link?.getAttribute("href") ?? "").not.toContain("javascript:");
    expect(window.__xssFired).toBeUndefined();
  });
});
