import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import { userEvent } from "@vitest/browser/context";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { paths } from "@/routes/paths";
import PinResetPage, { PIN_RESET_NOTICE } from "./PinResetPage";

// The page the emailed PIN reset link opens (MYSS-36), against a stand-in for
// POST /v1/pin-reset. The token is the one in the link's fragment.

const TOKEN = "0d7ad1a4-38d1-4a6e-9a0e-0f7c5bd3b2e1";

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

/** The sign-in page's place in the router: shows the notice it was given. */
function SignInStandIn() {
  const { state } = useLocation();
  return <p>Signed out: {(state as { notice?: string } | null)?.notice}</p>;
}

function stubReset(answer: Response | (() => Response)) {
  const calls: Array<{ body: unknown; headers: HeadersInit | undefined }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/pin-reset") && init?.method === "POST") {
      calls.push({
        body: JSON.parse(String(init.body)),
        headers: init.headers,
      });
      return typeof answer === "function" ? answer() : answer;
    }
    throw new Error(
      `Unexpected fetch in test: ${init?.method ?? "GET"} ${url}`,
    );
  });
  return calls;
}

function renderPage(hash = `#token=${TOKEN}`) {
  const queryClient = new QueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[`${paths.pinReset}${hash}`]}>
        <Routes>
          <Route path={paths.pinReset} element={<PinResetPage />} />
          <Route path={paths.signIn} element={<SignInStandIn />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

// Masked inputs have no textbox role; their labels end in "(required)".
const pinInput = (
  screen: Awaited<ReturnType<typeof renderPage>>,
  label: string,
) => screen.getByLabelText(new RegExp(`^${label}`));

afterEach(() => {
  vi.restoreAllMocks();
});

describe("PinResetPage", () => {
  it("sets the new PIN with the link's token, then sends the citizen to sign in", async () => {
    const calls = stubReset(() => new Response(null, { status: 204 }));
    const screen = await renderPage();

    await userEvent.fill(pinInput(screen, "New PIN"), "7350");
    await userEvent.fill(pinInput(screen, "Confirm New PIN"), "7350");
    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(screen.getByText(`Signed out: ${PIN_RESET_NOTICE}`))
      .toBeVisible();
    expect(calls).toHaveLength(1);
    expect(calls[0].body).toEqual({
      token: TOKEN,
      newPin: "7350",
      confirmPin: "7350",
    });
    // Anonymous: no sign-in token goes with it.
    expect(JSON.stringify(calls[0].headers)).not.toContain("Authorization");
  });

  it("catches a short PIN and a mismatch before the round trip", async () => {
    const calls = stubReset(() => new Response(null, { status: 204 }));
    const screen = await renderPage();

    await userEvent.fill(pinInput(screen, "New PIN"), "735");
    await screen.getByRole("button", { name: "Submit" }).click();
    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toBeVisible();

    await userEvent.fill(pinInput(screen, "New PIN"), "7350");
    await userEvent.fill(pinInput(screen, "Confirm New PIN"), "7351");
    await screen.getByRole("button", { name: "Submit" }).click();
    await expect
      .element(screen.getByRole("button", { name: /^Confirm New PIN:/ }))
      .toBeVisible();

    expect(calls).toHaveLength(0);
  });

  it("says when the link has expired or been used", async () => {
    stubReset(() =>
      json(410, {
        detail: "The link has expired or has already been used.",
        keyword: "ACCOUNT.PIN_RESET.LINK_INVALID",
      }),
    );
    const screen = await renderPage();

    await userEvent.fill(pinInput(screen, "New PIN"), "7350");
    await userEvent.fill(pinInput(screen, "Confirm New PIN"), "7350");
    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(
        screen.getByText("The link has expired or has already been used."),
      )
      .toBeVisible();
    expect(
      screen.getByRole("form", { name: "Reset your PIN" }).query(),
    ).toBeNull();
    await expect
      .element(screen.getByRole("link", { name: "Sign in to My Self Serve" }))
      .toBeVisible();
  });

  it("shows the API's field errors", async () => {
    stubReset(() =>
      json(422, {
        payload: [
          {
            field: "confirmPin",
            keyword: "IDA.PIN.MISMATCH",
            message: "The two PINs do not match.",
          },
        ],
      }),
    );
    const screen = await renderPage();

    await userEvent.fill(pinInput(screen, "New PIN"), "7350");
    await userEvent.fill(pinInput(screen, "Confirm New PIN"), "7350");
    await screen.getByRole("button", { name: "Submit" }).click();

    await expect
      .element(
        screen.getByRole("button", {
          name: "Confirm New PIN: The two PINs do not match.",
        }),
      )
      .toBeVisible();
  });

  it("explains a link without a token instead of showing the form", async () => {
    stubReset(() => new Response(null, { status: 204 }));
    const screen = await renderPage("");

    await expect
      .element(screen.getByText(/This link is not complete/))
      .toBeVisible();
    expect(
      screen.getByRole("form", { name: "Reset your PIN" }).query(),
    ).toBeNull();
  });
});
