// PERMANENT core (reused verbatim by Option 2). The typed identity the SPA
// shows in the UI. Authorization is always enforced server-side; the roles
// surfaced here are best-effort, for conditional rendering only.

export interface CurrentUser {
    sub: string;
    name?: string;
    email?: string;
    roles: string[];
    bceidGuid?: string;
    idirUsername?: string;
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

function asRoleArray(value: unknown): string[] {
    return Array.isArray(value)
        ? value.filter((r): r is string => typeof r === "string")
        : [];
}

// Collect roles from every place Keycloak may put them, de-duplicated:
//   - client_roles          (BC Gov standard-realm client-roles mapper)
//   - roles                 (already-flattened)
//   - realm_access.roles    (realm roles)
//   - resource_access.*.roles (per-client roles)
function collectRoles(claims: Claims): string[] {
    const roles = new Set<string>();

    asRoleArray(claims.client_roles).forEach((r) => roles.add(r));
    asRoleArray(claims.roles).forEach((r) => roles.add(r));

    const realmAccess = claims.realm_access as Claims | undefined;
    asRoleArray(realmAccess?.roles).forEach((r) => roles.add(r));

    const resourceAccess = claims.resource_access as
        | Record<string, Claims>
        | undefined;
    if (resourceAccess) {
        for (const client of Object.values(resourceAccess)) {
            asRoleArray(client?.roles).forEach((r) => roles.add(r));
        }
    }

    return [...roles];
}

export function normalizeUser(profile: Claims): CurrentUser {
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
        roles: collectRoles(profile),
        bceidGuid:
            asString(profile.bceid_user_guid) ?? asString(profile.bceid_guid),
        idirUsername: asString(profile.idir_username),
    };
}
