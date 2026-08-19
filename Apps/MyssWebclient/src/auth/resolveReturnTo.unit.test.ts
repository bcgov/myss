import { describe, it, expect } from "vitest";

import { resolveReturnTo } from "./resolveReturnTo";
import { paths } from "@/routes/paths";

/**
 * `state` round-trips through the identity provider, so it is treated as
 * attacker-influenceable input: the interesting cases here are the ones where
 * a plausible-looking value must NOT be followed.
 */
describe("resolveReturnTo", () => {
  it("honours a same-site rooted path", () => {
    expect(resolveReturnTo({ returnTo: "/techdemos/forms" })).toBe(
      "/techdemos/forms",
    );
  });

  it("keeps a query string and fragment on the path", () => {
    expect(resolveReturnTo({ returnTo: "/search?q=income#results" })).toBe(
      "/search?q=income#results",
    );
  });

  it.each([
    ["no state at all", undefined],
    ["null", null],
    ["state without returnTo", {}],
    ["a non-string returnTo", { returnTo: 42 }],
    ["an empty returnTo", { returnTo: "" }],
  ])("falls back home for %s", (_label, state) => {
    expect(resolveReturnTo(state)).toBe(paths.home);
  });

  it.each([
    ["an absolute http URL", "https://evil.example/steal"],
    ["a bare host", "evil.example"],
    ["a relative path", "dashboard"],
  ])("refuses %s", (_label, returnTo) => {
    expect(resolveReturnTo({ returnTo })).toBe(paths.home);
  });

  /**
   * The one that is easy to get wrong: "//evil.example" starts with "/" and so
   * passes a naive rooted-path check, but browsers read it as protocol-relative
   * and navigate off-site. Dropping the second condition in the guard would
   * turn the callback into an open redirect and only this case would notice.
   */
  it("refuses a protocol-relative URL", () => {
    expect(resolveReturnTo({ returnTo: "//evil.example/steal" })).toBe(
      paths.home,
    );
  });
});
