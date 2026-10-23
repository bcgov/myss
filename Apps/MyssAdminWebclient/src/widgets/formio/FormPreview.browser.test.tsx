import { afterEach, expect, test, vi } from "vitest";
import { render } from "vitest-browser-react";

import FormPreview from "./FormPreview";
import { EXAMPLE_SPEC } from "@myss/formio";

type Screen = Awaited<ReturnType<typeof render>>;

const RESULT_HEADING = "The form would submit. These answers would be sent:";

// Form.io sends its own requests through XMLHttpRequest, not fetch, so both are watched.
function watchRequests() {
  const fetchSpy = vi.spyOn(window, "fetch");
  const xhrSpy = vi.spyOn(XMLHttpRequest.prototype, "open");
  return {
    expectNone() {
      expect(fetchSpy).not.toHaveBeenCalled();
      expect(xhrSpy).not.toHaveBeenCalled();
    },
  };
}

async function renderPreview() {
  const requests = watchRequests();
  const screen = await render(<FormPreview spec={EXAMPLE_SPEC} />);
  await expect.element(fullName(screen)).toBeVisible();
  return { screen, requests };
}

function fullName(screen: Screen) {
  return screen.getByRole("textbox", { name: "Your full name" });
}

// The design system hides the real radio input, so click it directly.
function choose(screen: Screen, option: string) {
  (
    screen.getByRole("radio", { name: option }).element() as HTMLElement
  ).click();
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("shows the provider question only when the child is in licensed care", async () => {
  const { screen } = await renderPreview();
  const provider = screen.getByRole("textbox", {
    name: "Name of the care provider",
  });

  await expect.element(provider).not.toBeInTheDocument();

  choose(screen, "Yes");
  await expect.element(provider).toBeVisible();

  choose(screen, "No");
  await expect.element(provider).not.toBeInTheDocument();
});

test("lists the required questions when submitted empty, and shows no answers", async () => {
  const { screen, requests } = await renderPreview();

  await screen.getByRole("button", { name: "Submit" }).click();

  await expect.element(screen.getByText("There is a problem")).toBeVisible();
  await expect
    .element(screen.getByRole("button", { name: /Your full name/ }))
    .toBeVisible();
  expect(screen.getByText(RESULT_HEADING).query()).toBeNull();
  requests.expectNone();
});

test("shows the answers that would be sent, without sending them, and starts again", async () => {
  const { screen, requests } = await renderPreview();

  await fullName(screen).fill("Ada Lovelace");
  choose(screen, "Yes");
  const provider = screen.getByRole("textbox", {
    name: "Name of the care provider",
  });
  await expect.element(provider).toBeVisible();
  await provider.fill("Sunny Days Daycare");
  await expect.element(provider).toHaveValue("Sunny Days Daycare");
  await screen.getByRole("button", { name: "Submit" }).click();

  const heading = screen.getByRole("heading", { name: RESULT_HEADING });
  await expect.element(heading).toHaveFocus();
  const answers = JSON.parse(
    screen.getByText(/"applicantName"/).element().textContent ?? "",
  ) as Record<string, unknown>;
  expect(answers).toMatchObject({
    applicantName: "Ada Lovelace",
    inCare: "yes",
    careProvider: "Sunny Days Daycare",
  });

  await screen.getByRole("button", { name: "Start again" }).click();

  await expect.element(fullName(screen)).toHaveValue("");
  await expect
    .element(screen.getByRole("region", { name: "Form preview" }))
    .toHaveFocus();
  expect(screen.getByText(RESULT_HEADING).query()).toBeNull();
  requests.expectNone();
});

test("keeps the answers entered when the spec is unchanged", async () => {
  const { screen } = await renderPreview();

  await fullName(screen).fill("Ada Lovelace");
  await screen.rerender(<FormPreview spec={structuredClone(EXAMPLE_SPEC)} />);

  await expect.element(fullName(screen)).toHaveValue("Ada Lovelace");
});

test("a changed spec clears the answers and the problem list, without moving focus", async () => {
  const { screen } = await renderPreview();
  const changed = structuredClone(EXAMPLE_SPEC);
  (changed.components[0] as { label: string }).label = "Your legal name";

  await screen.getByRole("button", { name: "Submit" }).click();
  await expect.element(screen.getByText("There is a problem")).toBeVisible();
  const outside = document.createElement("button");
  document.body.append(outside);
  outside.focus();

  await screen.rerender(<FormPreview spec={changed} />);

  await expect
    .element(screen.getByRole("textbox", { name: "Your legal name" }))
    .toBeVisible();
  expect(screen.getByText("There is a problem").query()).toBeNull();
  expect(document.activeElement).toBe(outside);
  outside.remove();
});
