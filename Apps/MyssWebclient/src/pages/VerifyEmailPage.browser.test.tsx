import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter, Route, Routes } from "react-router";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { paths } from "@/routes/paths";
import VerifyEmailPage from "./VerifyEmailPage";

// The page the emailed verification link opens (MYSS-305), against a stand-in
// for POST /v1/email-verification. The token is the one in the link's fragment.

const TOKEN = "0d7ad1a4-38d1-4a6e-9a0e-0f7c5bd3b2e1";

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function stubVerify(answer: () => Response) {
  const calls: Array<{ body: unknown; headers: HeadersInit | undefined }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith("/v1/email-verification") && init?.method === "POST") {
      calls.push({
        body: JSON.parse(String(init.body)),
        headers: init.headers,
      });
      return answer();
    }
    throw new Error(
      `Unexpected fetch in test: ${init?.method ?? "GET"} ${url}`,
    );
  });
  return calls;
}

function renderPage(hash = `#token=${TOKEN}`) {
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={[`${paths.verifyEmail}${hash}`]}>
        <Routes>
          <Route path={paths.verifyEmail} element={<VerifyEmailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("VerifyEmailPage", () => {
  it("confirms only when asked, with the link's token and no sign-in", async () => {
    const calls = stubVerify(() => new Response(null, { status: 204 }));
    const screen = await renderPage();

    // Opening the page uses nothing up: a mail scanner may open it.
    await expect
      .element(screen.getByRole("button", { name: "Confirm my email address" }))
      .toBeVisible();
    expect(calls).toHaveLength(0);

    await screen
      .getByRole("button", { name: "Confirm my email address" })
      .click();

    await expect
      .element(screen.getByText(/Your email address is confirmed/))
      .toBeVisible();
    expect(calls).toHaveLength(1);
    expect(calls[0].body).toEqual({ token: TOKEN });
    expect(JSON.stringify(calls[0].headers)).not.toContain("Authorization");
  });

  it("says when the link has expired, been used or been replaced", async () => {
    stubVerify(() =>
      json(410, {
        detail:
          "The link has expired, has already been used, or a newer one was sent.",
        keyword: "ACCOUNT.EMAIL.LINK_INVALID",
      }),
    );
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Confirm my email address" })
      .click();

    await expect
      .element(
        screen.getByText(
          "The link has expired, has already been used, or a newer one was sent.",
        ),
      )
      .toBeVisible();
    await expect
      .element(screen.getByRole("link", { name: "Sign in to My Self Serve" }))
      .toBeVisible();
  });

  it("explains a link without a token instead of offering to confirm", async () => {
    stubVerify(() => new Response(null, { status: 204 }));
    const screen = await renderPage("");

    await expect
      .element(screen.getByText(/This link is not complete/))
      .toBeVisible();
    expect(
      screen.getByRole("button", { name: "Confirm my email address" }).query(),
    ).toBeNull();
  });

  // MYSS-295: a registration's link opens in a new window while the
  // registration waits, half-filled, in the first.
  describe("for a registration", () => {
    it("tells the citizen to close it and go back, and links nowhere", async () => {
      const calls = stubVerify(() => new Response(null, { status: 204 }));
      const screen = await renderPage(`?for=registration#token=${TOKEN}`);

      await screen
        .getByRole("button", { name: "Confirm my email address" })
        .click();

      await expect
        .element(
          screen.getByText(
            /You can close this window\. Go back to the window where you are creating your MySS account/,
          ),
        )
        .toBeVisible();
      expect(calls[0].body).toEqual({ token: TOKEN });
      expect(screen.getByRole("link").query()).toBeNull();
    });

    it("sends the citizen back to their registration for a new link", async () => {
      stubVerify(() =>
        json(410, {
          detail:
            "Sign in and choose Resend link on Account Info to get a new one.",
          keyword: "ACCOUNT.EMAIL.LINK_INVALID",
        }),
      );
      const screen = await renderPage(`?for=registration#token=${TOKEN}`);

      await screen
        .getByRole("button", { name: "Confirm my email address" })
        .click();

      await expect
        .element(
          screen.getByText(
            /Go back to the window where you are creating your MySS account and choose Resend link/,
          ),
        )
        .toBeVisible();
      expect(screen.getByText(/Account Info/).query()).toBeNull();
      expect(screen.getByRole("link").query()).toBeNull();
    });
  });
});
