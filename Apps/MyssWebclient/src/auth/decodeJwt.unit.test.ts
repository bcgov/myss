import { describe, it, expect } from "vitest";

import { decodeJwt } from "./decodeJwt";

// Build a JWT-shaped string with a real base64url payload and dummy
// header/signature. We only ever decode the payload, so the other two segments
// just need to be present.
// UTF-8-safe base64url: btoa() only accepts Latin1, so multibyte characters
// must be encoded to bytes first (mirrors how a real JWT is built).
function b64urlEncode(obj: unknown): string {
  const bytes = new TextEncoder().encode(JSON.stringify(obj));
  let binary = "";
  bytes.forEach((b) => (binary += String.fromCharCode(b)));
  return btoa(binary)
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

function makeJwt(payload: Record<string, unknown>): string {
  return `${b64urlEncode({ alg: "RS256", typ: "JWT" })}.${b64urlEncode(payload)}.sig`;
}

describe("decodeJwt", () => {
  it("decodes the payload of a well-formed JWT", () => {
    const token = makeJwt({
      sub: "abc-123",
      exp: 1735689600,
      roles: ["admin"],
    });
    const claims = decodeJwt(token);
    expect(claims).toEqual({
      sub: "abc-123",
      exp: 1735689600,
      roles: ["admin"],
    });
  });

  it("decodes multibyte UTF-8 claims correctly", () => {
    const token = makeJwt({ name: "Zoë Ünïcode 名前" });
    expect(decodeJwt(token)?.name).toBe("Zoë Ünïcode 名前");
  });

  it("returns null for an opaque (non-JWT) token", () => {
    expect(decodeJwt("opaque-refresh-token-value")).toBeNull();
  });

  it("returns null for a JWT with a malformed payload segment", () => {
    expect(decodeJwt("header.%%%not-base64%%%.sig")).toBeNull();
  });

  it("returns null when the payload is valid JSON but not an object", () => {
    // b64urlEncode(42) -> a JWT whose payload parses to the number 42.
    expect(decodeJwt(`h.${b64urlEncode(42)}.s`)).toBeNull();
  });

  it("returns null for empty, null or undefined input", () => {
    expect(decodeJwt("")).toBeNull();
    expect(decodeJwt(null)).toBeNull();
    expect(decodeJwt(undefined)).toBeNull();
  });
});
