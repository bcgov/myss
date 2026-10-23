import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import FormJsonDialog from "./FormJsonDialog";
import {
  FormLoadError,
  getFormVersion,
  publishForm,
  saveDraft,
} from "@/api/forms";
import type { FormSpecPayload } from "@/api/forms";
// The layout check needs the real spacing, which the design system's modal
// takes from these tokens.
import "@bcgov/design-tokens/css/variables.css";

// The dialog against a stubbed admin API: only the calls are mocked, and the
// real error classes are kept.

vi.mock("@/api/forms", async (importActual) => ({
  ...(await importActual<typeof import("@/api/forms")>()),
  getFormVersion: vi.fn(),
  saveDraft: vi.fn(),
  publishForm: vi.fn(),
}));

const mockGetFormVersion = vi.mocked(getFormVersion);

type Screen = Awaited<ReturnType<typeof render>>;

const VERSION_8: FormSpecPayload = {
  formSpecId: "bc-bus-pass",
  version: 8,
  title: "BC Bus Pass",
  spec: {
    display: "form",
    components: [
      { type: "textfield", key: "firstName", label: "First name", input: true },
    ],
  } as FormSpecPayload["spec"],
};

const JSON_8 = JSON.stringify(VERSION_8.spec, null, 2);

const HEADING = "BC Bus Pass · version 8 · Published";

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((settle) => {
    resolve = settle;
  });
  return { promise, resolve };
}

// A button that opens the dialog, as a version label on Form management does.
function Opener() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        v8 Published
      </button>
      {open && (
        <FormJsonDialog
          formSpecId="bc-bus-pass"
          formName="BC Bus Pass"
          version={8}
          isPublished
          isOpen
          onClose={() => setOpen(false)}
        />
      )}
    </>
  );
}

async function renderOpener() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, retryDelay: 0 } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <Opener />
    </QueryClientProvider>,
  );
}

async function open(screen: Screen) {
  await screen.getByRole("button", { name: "v8 Published" }).click();
}

function jsonBox(screen: Screen) {
  return screen.getByRole("textbox", { name: "Form JSON" });
}

afterEach(() => {
  vi.restoreAllMocks();
  vi.clearAllMocks();
});

describe("FormJsonDialog", () => {
  it("shows the version's heading and its JSON, read-only", async () => {
    mockGetFormVersion.mockResolvedValue(VERSION_8);
    const screen = await renderOpener();

    await open(screen);

    await expect
      .element(screen.getByRole("dialog", { name: HEADING }))
      .toBeVisible();
    await expect.element(jsonBox(screen)).toHaveValue(JSON_8);
    await expect.element(jsonBox(screen)).toHaveAttribute("readonly");
    expect(mockGetFormVersion).toHaveBeenCalledWith("bc-bus-pass", 8);
  });

  it("copies exactly the stored spec", async () => {
    mockGetFormVersion.mockResolvedValue(VERSION_8);
    const writeText = vi
      .spyOn(navigator.clipboard, "writeText")
      .mockResolvedValue();
    const screen = await renderOpener();
    await open(screen);

    await screen.getByRole("button", { name: "Copy" }).click();

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent("Copied.");
    expect(writeText).toHaveBeenCalledWith(JSON_8);
  });

  it("selects the JSON when the clipboard refuses", async () => {
    mockGetFormVersion.mockResolvedValue(VERSION_8);
    vi.spyOn(navigator.clipboard, "writeText").mockRejectedValue(
      new Error("Write permission denied."),
    );
    const screen = await renderOpener();
    await open(screen);

    await screen.getByRole("button", { name: "Copy" }).click();

    await expect
      .element(screen.getByRole("status"))
      .toHaveTextContent(/Couldn't copy automatically\. The JSON is selected/);
    await expect.element(jsonBox(screen)).toHaveFocus();
    const box = jsonBox(screen).element() as HTMLTextAreaElement;
    expect(box.selectionStart).toBe(0);
    expect(box.selectionEnd).toBe(JSON_8.length);
  });

  it("closes on Cancel and on Escape, and returns focus to the opener", async () => {
    mockGetFormVersion.mockResolvedValue(VERSION_8);
    const screen = await renderOpener();
    const opener = screen.getByRole("button", { name: "v8 Published" });

    await open(screen);
    await expect.element(jsonBox(screen)).toBeVisible();
    await screen.getByRole("button", { name: "Cancel" }).click();
    await expect.element(screen.getByRole("dialog")).not.toBeInTheDocument();
    await expect.element(opener).toHaveFocus();

    await open(screen);
    await expect.element(jsonBox(screen)).toBeVisible();
    await userEvent.keyboard("{Escape}");
    await expect.element(screen.getByRole("dialog")).not.toBeInTheDocument();
    await expect.element(opener).toHaveFocus();
  });

  it("keeps Tab inside the dialog", async () => {
    mockGetFormVersion.mockResolvedValue(VERSION_8);
    const screen = await renderOpener();
    await open(screen);
    await expect.element(jsonBox(screen)).toBeVisible();

    const dialog = screen.getByRole("dialog").element();
    for (let press = 0; press < 5; press += 1) {
      await userEvent.keyboard("{Tab}");
      expect(dialog.contains(document.activeElement)).toBe(true);
    }
  });

  it("shows a load error and writes nothing", async () => {
    mockGetFormVersion.mockRejectedValue(
      new FormLoadError(404, "Version fetch failed (404)"),
    );
    const screen = await renderOpener();

    await open(screen);

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Couldn't load version 8 (error 404).");
    expect(screen.getByRole("button", { name: "Copy" }).query()).toBeNull();
    expect(saveDraft).not.toHaveBeenCalled();
    expect(publishForm).not.toHaveBeenCalled();
  });

  it("reads the version again each time it opens", async () => {
    mockGetFormVersion.mockResolvedValueOnce(VERSION_8);
    const screen = await renderOpener();
    await open(screen);
    await expect.element(jsonBox(screen)).toHaveValue(JSON_8);
    await screen.getByRole("button", { name: "Cancel" }).click();

    const changed = {
      ...VERSION_8,
      spec: { ...VERSION_8.spec, components: [] },
    } as FormSpecPayload;
    const reread = deferred<FormSpecPayload>();
    mockGetFormVersion.mockReturnValueOnce(reread.promise);
    await open(screen);

    await expect.element(screen.getByText("Loading version 8…")).toBeVisible();
    expect(jsonBox(screen).query()).toBeNull();

    reread.resolve(changed);
    await expect
      .element(jsonBox(screen))
      .toHaveValue(JSON.stringify(changed.spec, null, 2));
    expect(mockGetFormVersion).toHaveBeenCalledTimes(2);
  });

  it("fits a short window, with Copy and Cancel inside the dialog", async () => {
    const { innerWidth, innerHeight } = window;
    await page.viewport(800, 450);
    try {
      const long = {
        ...VERSION_8,
        spec: {
          ...VERSION_8.spec,
          components: Array.from({ length: 30 }, (_, index) => ({
            type: "textfield",
            key: `field${index}`,
            label: `Field ${index}`,
            input: true,
          })),
        },
      } as FormSpecPayload;
      mockGetFormVersion.mockResolvedValue(long);
      const screen = await renderOpener();
      await open(screen);
      await expect
        .element(jsonBox(screen))
        .toHaveValue(JSON.stringify(long.spec, null, 2));

      const frame = screen
        .getByRole("dialog")
        .element()
        .parentElement!.getBoundingClientRect();
      expect(frame.top).toBeGreaterThanOrEqual(0);
      expect(frame.bottom).toBeLessThanOrEqual(window.innerHeight);
      for (const name of ["Copy", "Cancel"]) {
        const button = screen
          .getByRole("button", { name })
          .element()
          .getBoundingClientRect();
        expect(button.top).toBeGreaterThanOrEqual(frame.top);
        expect(button.bottom).toBeLessThanOrEqual(frame.bottom);
      }
      // The JSON box gives up the height, so the dialog itself need not scroll.
      const content = screen
        .getByRole("heading", { name: HEADING })
        .element().parentElement!;
      expect(content.scrollHeight).toBeLessThanOrEqual(content.clientHeight);
    } finally {
      await page.viewport(innerWidth, innerHeight);
    }
  });
});
