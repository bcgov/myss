import { describe, expect, it } from "vitest";

import { formatPhone, phoneDigits, phoneTypeLabel } from "./phone";

// The same cases as MyssApi.Tests/Domain/PhoneNumberTests.cs, so the browser
// and the API agree on what a phone number is. Every number is in the
// 555-01xx range reserved for fiction.
describe("phoneDigits", () => {
  it.each([
    ["(250) 555-0123"],
    ["250-555-0123"],
    ["250.555.0123"],
    ["2505550123"],
    [" 250 555 0123 "],
  ])("accepts %j", (input) => {
    expect(phoneDigits(input)).toBe("2505550123");
  });

  it.each([
    [""],
    ["555-0123"],
    ["1 250 555 0123"],
    ["250-555-01234"],
    ["250-555-0123 ext 4"],
    ["+1 250 555 0123"],
    ["250-CALL-NOW"],
  ])("refuses %j", (input) => {
    expect(phoneDigits(input)).toBeNull();
  });
});

describe("formatPhone", () => {
  it("shows ten digits in the Ministry format", () => {
    expect(formatPhone("2505550123")).toBe("(250) 555-0123");
  });

  it("leaves anything else as it is", () => {
    expect(formatPhone("555")).toBe("555");
  });
});

describe("phoneTypeLabel", () => {
  it("names the type the way the page shows it", () => {
    expect(phoneTypeLabel("Home")).toBe("Home Phone");
    expect(phoneTypeLabel("Message")).toBe("Message Phone");
  });
});
