import { describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";

import ErrorSummary, { type ErrorSummaryItem } from "./ErrorSummary";

type Screen = Awaited<ReturnType<typeof render>>;

const ITEMS: ErrorSummaryItem[] = [
  { message: "Duplicate field key: email", onSelect: () => {} },
  { message: 'A form with the ID "intake" already exists.' },
];

// A control outside the summary that holds focus, so a test can tell whether
// the summary took it.
function Harness({
  show = true,
  items = ITEMS,
  focusKey,
  announce = false,
}: Readonly<{
  show?: boolean;
  items?: readonly ErrorSummaryItem[];
  focusKey: string | number | null;
  announce?: boolean;
}>) {
  return (
    <>
      <button type="button">Elsewhere</button>
      {show && (
        <ErrorSummary items={items} focusKey={focusKey} announce={announce} />
      )}
    </>
  );
}

function heading(screen: Screen) {
  return screen.getByRole("heading", { name: "There is a problem" });
}

// Renders without the summary, focuses the outside control, then shows it.
async function showWithFocusElsewhere(focusKey: string | number | null) {
  const screen = await render(<Harness show={false} focusKey={focusKey} />);
  await screen.getByRole("button", { name: "Elsewhere" }).click();
  await screen.rerender(<Harness focusKey={focusKey} />);
  return screen;
}

describe("ErrorSummary", () => {
  it("renders an item with onSelect as a button and one without as text", async () => {
    const screen = await render(<Harness focusKey={null} />);

    await expect
      .element(
        screen.getByRole("button", { name: "Duplicate field key: email" }),
      )
      .toBeVisible();
    await expect
      .element(screen.getByText('A form with the ID "intake" already exists.'))
      .toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: /already exists/ }))
      .not.toBeInTheDocument();
  });

  it("calls an item's onSelect when it is selected", async () => {
    const onSelect = vi.fn();
    const screen = await render(
      <Harness
        items={[{ message: "Go to field", onSelect }]}
        focusKey={null}
      />,
    );

    await screen.getByRole("button", { name: "Go to field" }).click();

    expect(onSelect).toHaveBeenCalledTimes(1);
  });

  it("focuses the heading on mount when focusKey is set", async () => {
    const screen = await showWithFocusElsewhere("first");

    await expect.element(heading(screen)).toHaveFocus();
  });

  it("leaves focus where it is on mount when focusKey is null", async () => {
    const screen = await showWithFocusElsewhere(null);

    await expect.element(heading(screen)).toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Elsewhere" }))
      .toHaveFocus();
  });

  it("focuses the heading again when focusKey changes", async () => {
    const screen = await render(<Harness focusKey={1} />);
    await expect.element(heading(screen)).toHaveFocus();
    await screen.getByRole("button", { name: "Elsewhere" }).click();

    await screen.rerender(<Harness focusKey={2} />);

    await expect.element(heading(screen)).toHaveFocus();
  });

  it("does not move focus when the items change and focusKey does not", async () => {
    const screen = await render(<Harness focusKey={1} />);
    await expect.element(heading(screen)).toHaveFocus();
    await screen.getByRole("button", { name: "Elsewhere" }).click();

    await screen.rerender(
      <Harness items={[{ message: "Another problem" }]} focusKey={1} />,
    );

    await expect.element(screen.getByText("Another problem")).toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Elsewhere" }))
      .toHaveFocus();
  });

  it("is an alert only when announce is set", async () => {
    const quiet = await render(<Harness focusKey={null} />);
    await expect.element(heading(quiet)).toBeVisible();
    await expect.element(quiet.getByRole("alert")).not.toBeInTheDocument();
    await quiet.unmount();

    const announced = await render(<Harness focusKey={null} announce />);
    await expect
      .element(announced.getByRole("alert"))
      .toHaveTextContent("There is a problem");
  });

  it("renders nothing without items", async () => {
    const screen = await render(<Harness items={[]} focusKey={1} announce />);

    await expect
      .element(screen.getByRole("button", { name: "Elsewhere" }))
      .toBeVisible();
    await expect.element(heading(screen)).not.toBeInTheDocument();
    await expect.element(screen.getByRole("alert")).not.toBeInTheDocument();
  });

  it("does not take focus when items reappear under the same focusKey", async () => {
    // A caller that keeps it mounted: the problems were fixed, then a new one
    // is typed, with no new save attempt.
    const screen = await render(<Harness focusKey={1} />);
    await expect.element(heading(screen)).toHaveFocus();
    await screen.rerender(<Harness items={[]} focusKey={1} />);
    await screen.getByRole("button", { name: "Elsewhere" }).click();

    await screen.rerender(
      <Harness items={[{ message: "A new problem" }]} focusKey={1} />,
    );

    await expect.element(screen.getByText("A new problem")).toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Elsewhere" }))
      .toHaveFocus();
  });
});
