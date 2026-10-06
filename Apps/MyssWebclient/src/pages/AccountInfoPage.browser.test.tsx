import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router";
import { userEvent } from "@vitest/browser/context";
import { render } from "vitest-browser-react";
import { afterEach, describe, expect, it, vi } from "vitest";

import type { AccountPayload, PhoneInput } from "@/api/account";
import { phoneDigits } from "@/lib/phone";
import AccountInfoPage from "./AccountInfoPage";

// Account Info (MYSS-271) against a stand-in for the account API that keeps
// state: reads serve the account, writes change it the way the API does.
// Phone numbers are in the 555-01xx range reserved for fiction.

function account(overrides: Partial<AccountPayload> = {}): AccountPayload {
  return {
    caseNumber: "CASE_NUMBER_PLACEHOLDER",
    clientName: "Alex Deer",
    familyMembers: ["FAMILY_MEMBERS_PLACEHOLDER"],
    email: "alex@example.com",
    phones: [{ number: "2505550123", type: "Home" }],
    mailingAddressLines: ["MAILING_ADDRESS_PLACEHOLDER"],
    monthlyReportReminder: false,
    ...overrides,
  };
}

type Answer = { status: number; body: unknown };

interface Stub {
  account: AccountPayload;
  /** A canned answer for the next write; consumed once. */
  nextWrite?: Answer;
  /** A canned answer for the read. */
  read?: Answer;
}

function json(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function stubApi(stub: Stub) {
  const writes: Array<{ route: string; body: unknown }> = [];
  vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";

    if (url.endsWith("/v1/account") && method === "GET") {
      return stub.read
        ? json(stub.read.status, stub.read.body)
        : json(200, { payload: stub.account });
    }
    const route = /\/v1\/account\/(phones|notification-preferences)$/.exec(
      url,
    )?.[1];
    if (route && method === "PUT") {
      const body = JSON.parse(String(init?.body));
      writes.push({ route, body });
      if (stub.nextWrite) {
        const answer = stub.nextWrite;
        stub.nextWrite = undefined;
        return json(answer.status, answer.body);
      }
      stub.account =
        route === "phones"
          ? {
              ...stub.account,
              phones: (body.phones as PhoneInput[]).map((phone) => ({
                number: phoneDigits(phone.number)!,
                type: phone.type,
              })),
            }
          : { ...stub.account, ...body };
      return json(200, { payload: stub.account });
    }
    throw new Error(`Unexpected fetch in test: ${method} ${url}`);
  });
  return writes;
}

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AccountInfoPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe("AccountInfoPage", () => {
  it("shows the case lines, contact details, PIN and reminder sections", async () => {
    stubApi({ account: account() });
    const screen = await renderPage();

    await expect
      .element(screen.getByRole("heading", { level: 1, name: "Account Info" }))
      .toBeVisible();
    await expect
      .element(screen.getByText("CASE_NUMBER_PLACEHOLDER"))
      .toBeVisible();
    await expect.element(screen.getByText("Alex Deer")).toBeVisible();
    await expect
      .element(screen.getByText("FAMILY_MEMBERS_PLACEHOLDER"))
      .toBeVisible();

    await expect
      .element(screen.getByRole("textbox", { name: "Email Address" }))
      .toHaveValue("alex@example.com");
    const phone = screen.getByRole("group", { name: "Phone number 1" });
    await expect
      .element(phone.getByRole("textbox", { name: "Phone Number" }))
      .toHaveValue("(250) 555-0123");
    await expect
      .element(phone.getByRole("textbox", { name: "Type" }))
      .toHaveValue("Home Phone");
    await expect
      .element(screen.getByRole("textbox", { name: "Mailing Address" }))
      .toHaveValue("MAILING_ADDRESS_PLACEHOLDER");

    await expect
      .element(screen.getByRole("button", { name: "Change my PIN" }))
      .toBeVisible();
    await expect
      .element(screen.getByRole("button", { name: "Reset PIN" }))
      .toBeVisible();
    await expect.element(screen.getByRole("checkbox")).not.toBeChecked();
  });

  it("leaves out the case number line for a citizen with no case", async () => {
    stubApi({ account: account({ caseNumber: null, familyMembers: [] }) });
    const screen = await renderPage();

    await expect.element(screen.getByText("Alex Deer")).toBeVisible();
    await expect
      .element(screen.getByText("Case Number:"))
      .not.toBeInTheDocument();
    await expect
      .element(screen.getByText("Case Family Members:"))
      .not.toBeInTheDocument();
  });

  it("keeps email and mailing address read-only", async () => {
    stubApi({ account: account() });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();

    await expect
      .element(screen.getByRole("textbox", { name: "Email Address" }))
      .toHaveAttribute("readonly");
    await expect
      .element(screen.getByRole("textbox", { name: "Mailing Address" }))
      .toHaveAttribute("readonly");
  });

  it("edits, adds and saves phone numbers, then confirms", async () => {
    const writes = stubApi({ account: account() });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();
    const first = screen.getByRole("group", { name: "Phone number 1" });
    await first
      .getByRole("textbox", { name: "Phone Number" })
      .fill("604 555 0199");
    await screen.getByRole("button", { name: "Add phone number" }).click();
    const second = screen.getByRole("group", { name: "Phone number 2" });
    await second
      .getByRole("textbox", { name: "Phone Number" })
      .fill("250-555-0124");
    // A new row offers the first free type; choose another.
    await second.getByRole("button", { name: /Type/ }).click();
    await screen.getByRole("option", { name: "Work Phone" }).click();
    await screen.getByRole("button", { name: "Save" }).click();

    await expect
      .element(screen.getByText("Your contact information has been updated."))
      .toBeVisible();
    expect(writes).toEqual([
      {
        route: "phones",
        body: {
          phones: [
            { number: "604 555 0199", type: "Home" },
            { number: "250-555-0124", type: "Work" },
          ],
        },
      },
    ]);
    const saved = screen.getByRole("group", { name: "Phone number 2" });
    await expect
      .element(saved.getByRole("textbox", { name: "Phone Number" }))
      .toHaveValue("(250) 555-0124");
    await expect
      .element(saved.getByRole("textbox", { name: "Type" }))
      .toHaveValue("Work Phone");
    await expect
      .element(screen.getByRole("button", { name: "Edit contact information" }))
      .toHaveFocus();
  });

  it("refuses a number that is not ten digits before sending it", async () => {
    const writes = stubApi({ account: account() });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();
    const number = screen
      .getByRole("group", { name: "Phone number 1" })
      .getByRole("textbox", { name: "Phone Number" });
    await number.fill("555-0123");
    await screen.getByRole("button", { name: "Save" }).click();

    // Focus lands on the summary, and each entry takes you to its field.
    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
    await screen
      .getByRole("button", {
        name: "Phone number 1: Enter a 10-digit phone number with the area code, like (250) 555-0123.",
      })
      .click();
    await expect.element(number).toHaveFocus();
    await expect.element(number).toHaveAttribute("aria-invalid", "true");
    expect(writes).toEqual([]);
  });

  it("takes you from the summary to an invalid phone type", async () => {
    stubApi({ account: account() });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();
    await screen.getByRole("button", { name: "Add phone number" }).click();
    const second = screen.getByRole("group", { name: "Phone number 2" });
    await second
      .getByRole("textbox", { name: "Phone Number" })
      .fill("250-555-0124");
    await second.getByRole("button", { name: /Type/ }).click();
    await screen.getByRole("option", { name: "Home Phone" }).click();
    await screen.getByRole("button", { name: "Save" }).click();

    await screen
      .getByRole("button", { name: /^Phone number 2: You can have one/ })
      .click();
    await expect
      .element(second.getByRole("button", { name: /Type/ }))
      .toHaveFocus();
  });

  it("shows the API's field errors when it refuses the list", async () => {
    stubApi({
      account: account(),
      nextWrite: {
        status: 422,
        body: {
          payload: [
            {
              field: "phones[0].number",
              keyword: "ACCOUNT.PHONE.INVALID_FORMAT",
              message: "That number was refused upstream.",
            },
          ],
        },
      },
    });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();
    await screen.getByRole("button", { name: "Save" }).click();

    await expect
      .element(
        screen.getByRole("button", {
          name: "Phone number 1: That number was refused upstream.",
        }),
      )
      .toBeVisible();
    await expect
      .element(screen.getByRole("heading", { name: "There is a problem" }))
      .toHaveFocus();
    await expect
      .element(screen.getByRole("button", { name: "Save" }))
      .toBeVisible();
  });

  it("says so when a save fails for another reason", async () => {
    stubApi({ account: account(), nextWrite: { status: 500, body: {} } });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();
    await screen.getByRole("button", { name: "Save" }).click();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Your changes were not saved.");
  });

  it("removes a number and saves the empty list", async () => {
    const writes = stubApi({ account: account() });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();
    await screen.getByRole("button", { name: "Remove phone number 1" }).click();
    await screen.getByRole("button", { name: "Save" }).click();

    await expect
      .element(screen.getByText("No phone numbers added."))
      .toBeVisible();
    expect(writes).toEqual([{ route: "phones", body: { phones: [] } }]);
  });

  it("discards changes on Cancel", async () => {
    const writes = stubApi({ account: account() });
    const screen = await renderPage();

    await screen
      .getByRole("button", { name: "Edit contact information" })
      .click();
    await screen
      .getByRole("group", { name: "Phone number 1" })
      .getByRole("textbox", { name: "Phone Number" })
      .fill("604 555 0199");
    await screen.getByRole("button", { name: "Cancel" }).click();

    await expect
      .element(
        screen
          .getByRole("group", { name: "Phone number 1" })
          .getByRole("textbox", { name: "Phone Number" }),
      )
      .toHaveValue("(250) 555-0123");
    expect(writes).toEqual([]);
  });

  it("saves the monthly report reminder when the box changes", async () => {
    const writes = stubApi({ account: account() });
    const screen = await renderPage();

    await userEvent.click(screen.getByText(/Please send me a reminder/));

    await expect
      .element(screen.getByText("Your preference has been saved."))
      .toBeVisible();
    await expect.element(screen.getByRole("checkbox")).toBeChecked();
    expect(writes).toEqual([
      {
        route: "notification-preferences",
        body: { monthlyReportReminder: true },
      },
    ]);
  });

  it("says so when the account cannot be loaded", async () => {
    stubApi({ account: account(), read: { status: 403, body: {} } });
    const screen = await renderPage();

    await expect
      .element(screen.getByRole("alert"))
      .toHaveTextContent("Could not load your account");
  });
});
