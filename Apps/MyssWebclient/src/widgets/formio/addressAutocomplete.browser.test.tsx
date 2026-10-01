import { Form } from "@formio/react";
import type { FormType } from "@formio/react/lib/components/Form";
import { userEvent } from "@vitest/browser/context";
import { render } from "vitest-browser-react";
import { afterEach, expect, test, vi } from "vitest";

import { registerBcgovComponents } from "./bcgovComponents";

vi.mock("@/constants", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/constants")>()),
  CANADA_POST_API_KEY: "test-canada-post-key",
}));

const addressFields = {
  line2: "streetAddress2",
  city: "city",
  province: "province",
  postalCode: "postalCode",
};

function addressSpec(fields: Record<string, string> = addressFields): FormType {
  // An assertion, because Form.io's Component type has no custom properties.
  return {
    display: "form",
    components: [
      {
        type: "bcgovAddressAutocomplete",
        key: "streetAddress1",
        label: "Street address line 1",
        input: true,
        validate: { required: true },
        addressFields: fields,
      },
      {
        type: "textfield",
        key: "streetAddress2",
        label: "Street address line 2",
        input: true,
      },
      {
        type: "textfield",
        key: "city",
        label: "City",
        input: true,
        validate: { required: true },
      },
      {
        type: "textfield",
        key: "province",
        label: "Province",
        input: true,
        validate: {
          pattern: "^(BC|British Columbia)$",
          required: true,
          customMessage: "An address in BC is required",
        },
      },
      {
        type: "textfield",
        key: "postalCode",
        label: "Postal code",
        input: true,
        errors: { pattern: "Invalid postal code format" },
        validate: {
          pattern: "^[A-Za-z][0-9][A-Za-z] ?[0-9][A-Za-z][0-9]$",
          required: true,
        },
      },
      {
        type: "button",
        key: "submit",
        action: "submit",
        label: "Submit",
        input: true,
      },
    ],
  } as FormType;
}

const building = {
  Id: "CAN|building",
  Text: "501 Belleville St",
  Description: "2 Addresses",
  Next: "Find",
};
const unit = {
  Id: "CAN|unit-12",
  Text: "12-501 Belleville St",
  Description: "Victoria, BC, V8V 1X4",
  Next: "Retrieve",
};
const unitAddress = {
  Language: "ENG",
  Line1: "12-501 BELLEVILLE ST",
  Line2: "REAR ENTRANCE",
  City: "VICTORIA",
  ProvinceCode: "BC",
  PostalCode: "V8V 1X4",
};

/** Holds a stubbed response until the test calls `release()`. */
function gate() {
  let release!: () => void;
  const opened = new Promise<void>((resolve) => (release = resolve));
  return { opened, release };
}

/** Waits on `held` like a slow network, rejecting on abort as real fetch does. */
function respondAfter(
  held: Promise<void> | undefined,
  signal: AbortSignal | null | undefined,
): Promise<void> | undefined {
  if (!held) return undefined;
  return new Promise((resolve, reject) => {
    signal?.addEventListener("abort", () =>
      reject(new DOMException("Aborted", "AbortError")),
    );
    void held.then(resolve);
  });
}

function stubCanadaPost({
  find = [[]],
  retrieve = [unitAddress],
  findGate,
  retrieveGate,
}: {
  find?: unknown[][];
  retrieve?: unknown[];
  findGate?: Promise<void>;
  retrieveGate?: Promise<void>;
}) {
  let findCalls = 0;
  return vi.spyOn(window, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    let items: unknown[];
    if (url.includes("/AddressComplete/Interactive/Find/")) {
      items = find[Math.min(findCalls++, find.length - 1)];
      await respondAfter(findGate, init?.signal);
    } else if (url.includes("/AddressComplete/Interactive/Retrieve/")) {
      items = retrieve;
      await respondAfter(retrieveGate, init?.signal);
    } else {
      throw new Error(`Unexpected fetch in test: ${url}`);
    }
    return new Response(JSON.stringify({ Items: items }), {
      status: 200,
      headers: { "Content-Type": "application/json" },
    });
  });
}

registerBcgovComponents();

function renderAddressForm(spec = addressSpec()) {
  const submissions: Record<string, unknown>[] = [];
  const screen = render(
    <Form
      src={spec}
      onSubmit={(submission: { data: Record<string, unknown> }) => {
        submissions.push(submission.data);
      }}
    />,
  );
  return { screen, submissions };
}

afterEach(() => {
  vi.restoreAllMocks();
});

test("drills into a building and fills every field from the chosen unit", async () => {
  stubCanadaPost({ find: [[building], [unit]] });
  const { screen: rendered, submissions } = renderAddressForm();
  const screen = await rendered;
  const line1 = screen.getByRole("combobox", { name: "Street address line 1" });

  await line1.fill("5");
  await screen.getByRole("option", { name: /501 Belleville St/ }).click();
  await screen.getByRole("option", { name: /12-501 Belleville St/ }).click();

  await expect.element(line1).toHaveValue("12-501 BELLEVILLE ST");
  await screen.getByRole("button", { name: "Submit" }).click();

  await vi.waitFor(() => expect(submissions).toHaveLength(1));
  expect(submissions[0]).toMatchObject({
    streetAddress1: "12-501 BELLEVILLE ST",
    streetAddress2: "REAR ENTRANCE",
    city: "VICTORIA",
    province: "BC",
    postalCode: "V8V 1X4",
  });
});

test("allows manual entry and keeps the spec's field validation", async () => {
  stubCanadaPost({ find: [[]] });
  const { screen: rendered, submissions } = renderAddressForm();
  const screen = await rendered;

  await screen
    .getByRole("combobox", { name: "Street address line 1" })
    .fill("PO Box 123");
  await screen.getByRole("textbox", { name: "City" }).fill("Victoria");
  await screen.getByRole("textbox", { name: "Province" }).fill("AB");
  await screen.getByRole("textbox", { name: "Postal code" }).fill("invalid");
  await screen.getByRole("button", { name: "Submit" }).click();

  await expect
    .element(screen.getByText("An address in BC is required").first())
    .toBeVisible();
  await expect
    .element(screen.getByText("Invalid postal code format").first())
    .toBeVisible();
  expect(submissions).toHaveLength(0);

  await screen.getByRole("textbox", { name: "Province" }).fill("BC");
  await screen.getByRole("textbox", { name: "Postal code" }).fill("V8W 9R6");
  await screen.getByRole("button", { name: "Submit" }).click();

  await vi.waitFor(() => expect(submissions).toHaveLength(1));
  expect(submissions[0]).toMatchObject({
    streetAddress1: "PO Box 123",
    city: "Victoria",
    province: "BC",
    postalCode: "V8W 9R6",
  });
});

test("keeps address fields the user edits while the chosen address loads", async () => {
  const retrieveGate = gate();
  stubCanadaPost({ find: [[unit]], retrieveGate: retrieveGate.opened });
  const { screen: rendered } = renderAddressForm();
  const screen = await rendered;
  const city = screen.getByRole("textbox", { name: "City" });

  await screen
    .getByRole("combobox", { name: "Street address line 1" })
    .fill("5");
  await screen.getByRole("option", { name: /12-501 Belleville St/ }).click();
  await city.fill("Nanaimo");
  retrieveGate.release();

  await expect
    .element(screen.getByText(/Your address changes were kept/))
    .toBeVisible();
  await expect.element(city).toHaveValue("Nanaimo");
  await expect
    .element(screen.getByRole("textbox", { name: "Postal code" }))
    .toHaveValue("");
});

test("does not reopen suggestions that arrive after the field loses focus", async () => {
  const findGate = gate();
  const fetchSpy = stubCanadaPost({
    find: [[unit]],
    findGate: findGate.opened,
  });
  const { screen: rendered } = renderAddressForm();
  const screen = await rendered;
  const line1 = screen.getByRole("combobox", { name: "Street address line 1" });

  await line1.fill("5");
  await vi.waitFor(() => expect(fetchSpy).toHaveBeenCalled());
  await screen.getByRole("textbox", { name: "City" }).click();
  findGate.release();

  // Form.io has its own page-wide status region, so read the one beside line 1.
  const addressStatus = () =>
    line1.element().parentElement?.querySelector('[role="status"]')
      ?.textContent;
  await vi.waitFor(() => expect(addressStatus()).toBe(""));
  expect(screen.getByRole("option").elements()).toHaveLength(0);
  await expect.element(line1).toHaveAttribute("aria-expanded", "false");
});

test("Escape cancels a search in flight so the list stays closed", async () => {
  const findGate = gate();
  const fetchSpy = stubCanadaPost({
    find: [[unit]],
    findGate: findGate.opened,
  });
  const { screen: rendered } = renderAddressForm();
  const screen = await rendered;
  const line1 = screen.getByRole("combobox", { name: "Street address line 1" });

  await line1.fill("5");
  await vi.waitFor(() => expect(fetchSpy).toHaveBeenCalled());
  await userEvent.keyboard("{Escape}");
  findGate.release();

  await expect(fetchSpy.mock.results[0].value).rejects.toThrow("Aborted");
  expect(screen.getByRole("option").elements()).toHaveLength(0);
  await expect.element(line1).toHaveAttribute("aria-expanded", "false");
});

test("stays a plain text field when addressFields names a missing field", async () => {
  const consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
  stubCanadaPost({});
  const { screen: rendered } = renderAddressForm(
    addressSpec({ ...addressFields, city: "noSuchField" }),
  );
  const screen = await rendered;

  await expect
    .element(screen.getByRole("textbox", { name: "Street address line 1" }))
    .toBeVisible();
  expect(consoleError).toHaveBeenCalledWith(
    expect.stringContaining("invalid addressFields mapping"),
  );
});
