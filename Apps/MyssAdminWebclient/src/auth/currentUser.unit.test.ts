import { describe, expect, it } from "vitest";

import { normalizeUser } from "./currentUser";

describe("normalizeUser", () => {
  it("reads the IDIR username and display identity from the token", () => {
    expect(
      normalizeUser({
        sub: "abc@azureidir",
        display_name: "Doe, Jane CITZ:EX",
        email: "jane.doe@gov.bc.ca",
        idir_username: "JDOE",
      }),
    ).toEqual({
      sub: "abc@azureidir",
      name: "Doe, Jane CITZ:EX",
      email: "jane.doe@gov.bc.ca",
      idirUsername: "JDOE",
    });
  });

  it("leaves the IDIR username unset for a non-IDIR token", () => {
    expect(
      normalizeUser({ sub: "x@bceidboth", given_name: "Pat" }).idirUsername,
    ).toBeUndefined();
  });

  it("joins given and family names when there is no display name", () => {
    expect(
      normalizeUser({ sub: "s", given_name: "Jane", family_name: "Doe" }).name,
    ).toBe("Jane Doe");
  });
});
