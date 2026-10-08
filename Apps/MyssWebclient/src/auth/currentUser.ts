// PERMANENT core (reused verbatim by Option 2). The typed identity the SPA
// shows in the UI. Authorization is always enforced server-side. Roles are
// server-computed (GET /v1/auth/me -> RoleCalculator, ADR-0007) and merged in
// by buildSession — normalizeUser deliberately does not read role-shaped
// token claims, so there is exactly one place roles are decided.

export interface CurrentUser {
  sub: string;
  name?: string;
  email?: string;
  roles: string[];
  bceidGuid?: string;
  idirUsername?: string;
  // Identity details the registration form prefills from. Each is present only
  // when the identity provider released the claim; BC Services Card can carry
  // all of them, BCeID and IDIR typically only the names and email. They are a
  // convenience for the citizen, not proof — the API stores what is submitted.
  givenName?: string;
  familyName?: string;
  phoneNumber?: string;
  birthdate?: string;
  gender?: string;
}

// Shape of the OIDC id-token claims we read. Kept loose because BC Gov's
// standard realm spells identity claims a few different ways.
type Claims = Record<string, unknown>;

function asString(value: unknown): string | undefined {
  return typeof value === "string" && value.length > 0 ? value : undefined;
}

// Builds a display name from the separate given/family claims. BC Services
// Card spells the first one `given_names` (plural, and it can hold more than
// one name); IDIR and Basic BCeID use `given_name`. Returns undefined rather
// than a half-name when neither side is present.
function joinGivenAndFamily(claims: Claims): string | undefined {
  const given = asString(claims.given_name) ?? asString(claims.given_names);
  const family = asString(claims.family_name);
  const joined = [given, family].filter(Boolean).join(" ");
  return joined.length > 0 ? joined : undefined;
}

// The id-token half of CurrentUser: what the token is genuinely authoritative
// for — display identity and the keystone identifiers. Roles are the server's
// half (see the header comment).
export function normalizeUser(profile: Claims): Omit<CurrentUser, "roles"> {
  return {
    sub: asString(profile.sub) ?? "",
    // Deliberately NOT falling back to preferred_username: BC Services
    // Card issues an opaque directed identifier there (a 44-char base64
    // pseudonym), and greeting a citizen with that is worse than greeting
    // them with no name at all. Callers render "Welcome back" unadorned
    // when this is undefined.
    name:
      asString(profile.name) ??
      asString(profile.display_name) ??
      joinGivenAndFamily(profile),
    email: asString(profile.email),
    bceidGuid:
      asString(profile.bceid_user_guid) ?? asString(profile.bceid_guid),
    idirUsername: asString(profile.idir_username),
    givenName: asString(profile.given_name) ?? asString(profile.given_names),
    familyName: asString(profile.family_name),
    // Standard OIDC claim names; BC Services Card releases these only when the
    // Keycloak client is configured for them.
    phoneNumber: asString(profile.phone_number),
    birthdate: asString(profile.birthdate),
    gender: asString(profile.gender),
  };
}
