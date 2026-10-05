import { describe, expect, it } from "vitest";

import { formatPhone, prepareRegistrationSpec } from "./registrationSpec";

type Component = Record<string, unknown>;

function field(key: string, attribute: string, lock = true): Component {
  return {
    type: "textfield",
    key,
    input: true,
    properties: lock
      ? { myssPrefill: attribute, myssPrefillLock: "true" }
      : { myssPrefill: attribute },
  };
}

const gender: Component = {
  type: "bcgovRadio",
  key: "gender",
  input: true,
  values: [
    { label: "Man/Boy", value: "man" },
    { label: "Non-Binary", value: "nonBinary" },
    { label: "Woman/Girl", value: "woman" },
  ],
  properties: { myssPrefill: "gender", myssPrefillLock: "true" },
};

function components(spec: unknown): Component[] {
  return (spec as { components: Component[] }).components;
}

function byKey(spec: unknown, key: string): Component {
  const found = components(spec).find((c) => c.key === key);
  if (!found) throw new Error(`no component "${key}"`);
  return found;
}

describe("prepareRegistrationSpec", () => {
  const spec = {
    display: "form",
    components: [
      field("firstName", "givenName"),
      field("lastName", "familyName"),
      field("email", "email", false),
      field("phone", "phoneNumber"),
      field("dateOfBirth", "birthdate"),
      gender,
      { type: "textfield", key: "sin", input: true },
    ],
  };

  it("fills and locks the fields the identity supplied", () => {
    const prepared = prepareRegistrationSpec(spec, {
      givenName: "Jane",
      familyName: "Johnson",
      phoneNumber: "+1 778 123 4567",
      birthdate: "1990-01-01",
      gender: "female",
    });

    expect(byKey(prepared, "firstName")).toMatchObject({
      defaultValue: "Jane",
      disabled: true,
    });
    expect(byKey(prepared, "lastName").defaultValue).toBe("Johnson");
    expect(byKey(prepared, "phone").defaultValue).toBe("778-123-4567");
    // Local midnight, so the picker shows 1990-01-01 in every time zone.
    expect(byKey(prepared, "dateOfBirth").defaultValue).toBe(
      "1990-01-01T00:00:00",
    );
    expect(byKey(prepared, "gender")).toMatchObject({
      defaultValue: "woman",
      disabled: true,
    });
  });

  it("fills email but leaves it editable when the spec does not lock it", () => {
    const prepared = prepareRegistrationSpec(spec, {
      email: "jane@example.com",
    });

    expect(byKey(prepared, "email").defaultValue).toBe("jane@example.com");
    expect(byKey(prepared, "email").disabled).toBeUndefined();
  });

  // The lock only applies to a filled field; otherwise a missing claim would
  // leave a required field the citizen cannot complete.
  it("leaves a lockable field editable when the identity did not supply it", () => {
    const prepared = prepareRegistrationSpec(spec, { givenName: "Jane" });

    for (const key of ["lastName", "phone", "dateOfBirth", "gender"]) {
      expect(byKey(prepared, key).defaultValue).toBeUndefined();
      expect(byKey(prepared, key).disabled).toBeUndefined();
    }
  });

  it.each([
    [
      "a phone number that is not ten digits",
      { phoneNumber: "555-0100" },
      "phone",
    ],
    ["a year-only birthdate", { birthdate: "1990" }, "dateOfBirth"],
    ["an unknown gender", { gender: "unknown" }, "gender"],
  ])("does not fill or lock %s", (_case, identity, key) => {
    const prepared = prepareRegistrationSpec(spec, identity);

    expect(byKey(prepared, key).defaultValue).toBeUndefined();
    expect(byKey(prepared, key).disabled).toBeUndefined();
  });

  // A misspelled marker in Strapi must not lock an empty field.
  it("ignores a prefill marker it does not recognise", () => {
    const prepared = prepareRegistrationSpec(
      { components: [field("middleName", "middleNames")] },
      { givenName: "Jane" },
    );

    expect(byKey(prepared, "middleName").defaultValue).toBeUndefined();
    expect(byKey(prepared, "middleName").disabled).toBeUndefined();
  });

  it("maps the BC Services Card gender values to the form's options", () => {
    const fill = (value: string) =>
      byKey(prepareRegistrationSpec(spec, { gender: value }), "gender")
        .defaultValue;

    expect(fill("male")).toBe("man");
    expect(fill("FEMALE")).toBe("woman");
    expect(fill("diverse")).toBe("nonBinary");
  });

  it("removes submit buttons, including inside panels, and keeps the rest", () => {
    const prepared = prepareRegistrationSpec(
      {
        components: [
          {
            type: "panel",
            key: "p",
            components: [
              field("firstName", "givenName"),
              { type: "button", key: "submit", action: "submit" },
            ],
          },
          { type: "button", key: "submit2" },
          { type: "button", key: "reset", action: "reset" },
        ],
      },
      { givenName: "Jane" },
    );

    expect(components(prepared).map((c) => c.key)).toEqual(["p", "reset"]);
    const panel = byKey(prepared, "p").components as Component[];
    expect(panel.map((c) => c.key)).toEqual(["firstName"]);
    expect(panel[0]?.defaultValue).toBe("Jane");
  });

  it("does not mutate the spec it is given", () => {
    const before = JSON.stringify(spec);
    prepareRegistrationSpec(spec, { givenName: "Jane", gender: "male" });

    expect(JSON.stringify(spec)).toBe(before);
  });
});

describe("formatPhone", () => {
  it.each([
    ["7781234567", "778-123-4567"],
    ["(778) 123-4567", "778-123-4567"],
    ["+1 778 123 4567", "778-123-4567"],
  ])("formats %s", (raw, formatted) => {
    expect(formatPhone(raw)).toBe(formatted);
  });

  it("rejects numbers that are not ten North American digits", () => {
    expect(formatPhone("123-4567")).toBeUndefined();
    expect(formatPhone("+44 20 7946 0958")).toBeUndefined();
  });
});
