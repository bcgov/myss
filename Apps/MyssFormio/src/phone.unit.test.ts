import { describe, expect, it } from "vitest";

import { formatPhone, formatPhoneInput, isPhoneField, phoneDigits, phoneTypeLabel } from "./phone";

// The same cases as MyssFormSpec.Tests/Domain/PhoneNumberTests.cs, so the browser
// and the API agree on what a phone number is. Every number is in the
// 555-01xx range reserved for fiction.
describe("phoneDigits", () => {
  it.each([
    ["(250) 555-0123"],
    ["250-555-0123"],
    ["250.555.0123"],
    ["2505550123"],
    ["(250 555-0123"],
    [" (2 5 0) 5 5 5 - 0 1 2 3 \n"],
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
    ["1505550123"],
    ["25055501234"],
    ["2505550123x"],
    ["250(555)-0123"],
    ["250--555-0123"],
  ])("refuses %j", (input) => {
    expect(phoneDigits(input)).toBeNull();
  });
});

describe("formatPhoneInput", () => {
  it.each([
    ["2", "(2"],
    ["250", "(250)"],
    ["2505", "(250) 5"],
    ["250555", "(250) 555"],
    ["2505550123", "(250) 555-0123"],
    [" \t(250)  555 - 0123  ", "(250) 555-0123"],
    ["25055501234", "(250) 555-0123"],
    ["1 250 555 0123 4", "(125) 055-5012"],
    ["2505550123x", "2505550123x"],
  ])("formats %j as %j", (input, expected) => {
    expect(formatPhoneInput(input)).toBe(expected);
  });
});

describe("isPhoneField", () => {
  it.each([
    [{ type: "textfield", properties: { myssValidator: "phone" } }, true],
    [{ type: "phoneNumber" }, true],
    [{ type: "textfield", inputMask: "(999) 999-9999" }, true],
    [{ type: "textfield", inputMask: "999-999-9999" }, true],
    [{ type: "textfield", inputMask: "999999999" }, false],
    [{ type: "textfield", inputMask: "9999" }, false],
    [{ type: "textfield", inputMask: "(999) 999-9999", properties: { myssValidator: "sin" } }, false],
  ])("classifies %j as %j", (component, expected) => {
    expect(isPhoneField(component)).toBe(expected);
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
