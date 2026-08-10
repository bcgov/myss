// A tiny, dependency-free JWT decoder for the SimpleLogin harness.
//
// WHY THIS EXISTS: react-oidc-context / oidc-client-ts only decodes the ID
// token for us (it lands on `auth.user.profile`). The access token stays a raw
// string, and refresh tokens are often opaque. To show the *decoded* claims the
// way the BC Gov keycloak playground does, we decode the JWT payload ourselves.
//
// This never throws. A malformed or opaque token (e.g. a non-JWT refresh
// token) returns null so callers can show a graceful "not a JWT" note instead
// of blowing up the page.

export type JwtClaims = Record<string, unknown>;

// base64url -> UTF-8 string. Handles the '-'/'_' alphabet, missing padding, and
// multibyte characters (via decodeURIComponent over the percent-escaped bytes).
function base64UrlDecode(segment: string): string {
  let base64 = segment.replace(/-/g, "+").replace(/_/g, "/");
  const pad = base64.length % 4;
  if (pad === 2) base64 += "==";
  else if (pad === 3) base64 += "=";
  else if (pad === 1) throw new Error("Invalid base64url length");

  const binary = atob(base64);
  // Re-encode each byte as a percent escape so decodeURIComponent can rebuild
  // any multibyte UTF-8 sequences correctly.
  const percent = Array.from(binary)
    .map((ch) => "%" + ch.charCodeAt(0).toString(16).padStart(2, "0"))
    .join("");
  return decodeURIComponent(percent);
}

// Decode the payload (middle segment) of a JWT into a claims object.
// Returns null for anything that is not a well-formed three-part JWT.
export function decodeJwt(token: string | undefined | null): JwtClaims | null {
  if (!token) return null;

  const parts = token.split(".");
  if (parts.length !== 3) return null;

  try {
    const json = base64UrlDecode(parts[1]);
    const claims = JSON.parse(json);
    // Guard against valid-JSON-but-not-an-object payloads (e.g. "42").
    return claims && typeof claims === "object" ? (claims as JwtClaims) : null;
  } catch {
    return null;
  }
}
