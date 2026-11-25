import { beforeAll, describe, expect, test } from "vitest";
import { page } from "vitest/browser";
import { render } from "vitest-browser-react";
import { MemoryRouter } from "react-router";

import { loadPageStyles } from "@/testing/pageStyles";
import DesignSystemTechDemo from "./DesignSystemTechDemo";

async function renderPage() {
  return render(
    <MemoryRouter>
      <DesignSystemTechDemo />
    </MemoryRouter>,
  );
}

describe("DesignSystemTechDemo", () => {
  beforeAll(async () => {
    await loadPageStyles();
  });

  test("lists every section, and each link reaches its section", async () => {
    const screen = await renderPage();
    const contents = screen.getByRole("navigation", { name: "On this page" });

    for (const name of [
      "Design tokens",
      "Typography",
      "Buttons and links",
      "Form fields",
      "Radio buttons, checkboxes and dropdowns",
      "Accordions",
      "Alerts, notifications and status",
      "Cards and information panels",
      "Navigation and page headers",
      "Tables and lists",
      "Modals and dialogs",
      "Loading, empty, error and unavailable states",
    ]) {
      const link = contents.getByRole("link", { name, exact: true });
      const target = link.element().getAttribute("href")!.slice(1);
      await expect
        .element(screen.getByRole("region", { name, exact: true }))
        .toHaveAttribute("id", target);
    }
  });

  test("shows a token's resolved value beside its name", async () => {
    const screen = await renderPage();
    const swatch = screen
      .getByText("--surface-color-primary-button-default", { exact: true })
      .element().parentElement!;
    // The design system's BC blue; empty would mean the tokens never loaded.
    expect(swatch.textContent).toMatch(/#013366/i);
  });

  test("draws fields in their error and disabled states", async () => {
    const screen = await renderPage();
    const fields = screen.getByRole("region", { name: "Form fields" });

    await expect
      .element(fields.getByText("Enter a valid value."))
      .toBeVisible();
    await expect
      .element(fields.getByRole("textbox", { name: "Input label" }).nth(3))
      .toBeDisabled();
  });

  test("opens a dialog and closes it on Cancel", async () => {
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Open destructive dialog" })
      .click();
    const dialog = page.getByRole("dialog", { name: "Dialog title" });
    await expect.element(dialog).toBeVisible();

    await dialog.getByRole("button", { name: "Cancel" }).click();
    await expect.element(dialog).not.toBeInTheDocument();
  });

  test("shows the error summary only when asked", async () => {
    const screen = await renderPage();

    await expect
      .element(screen.getByText("There is a problem"))
      .not.toBeInTheDocument();
    await screen
      .getByRole("button", { name: "Show the error summary" })
      .click();
    await expect
      .element(screen.getByText("Enter your first name."))
      .toBeVisible();
  });
});
