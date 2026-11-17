// The typed identity the admin app shows in the UI, read from the id token.
// Authorization is always enforced server-side: MyssAdminApi's AdminIdir
// policy reads the same `idir_username` claim from the access token, so the
// app needs no /auth/me round trip to know whether the caller is IDIR.

export interface CurrentUser {
  sub: string;
  name?: string;
  email?: string;
  idirUsername?: string;
}

// Shape of the OIDC id-token claims we read. Kept loose because BC Gov's
// standard realm spells identity claims a few different ways.
type Claims = Record<string, unknown>;

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

function joinGivenAndFamily(claims: Claims): string | undefined {
  const given = asString(claims.given_name);
  const family = asString(claims.family_name);
  const joined = [given, family].filter(Boolean).join(" ");
  return joined.length > 0 ? joined : undefined;
}

export function normalizeUser(profile: Claims): CurrentUser {
  return {
    sub: asString(profile.sub) ?? "",
    name:
      asString(profile.name) ??
      asString(profile.display_name) ??
      joinGivenAndFamily(profile),
    email: asString(profile.email),
    idirUsername: asString(profile.idir_username),
  };
}
