namespace Myss.Api.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Text.Json;

    /// <summary>
    /// Where Keycloak carries this realm's roles. BC Gov's standard realm can be configured
    /// either way, so this stays switchable (Oidc:RoleSource) until IDIM confirms the model.
    /// Reading both is the safe default: a role that is absent simply contributes nothing.
    /// </summary>
    public enum KeycloakRoleSource
    {
        /// <summary>Realm roles only (<c>realm_access.roles</c>).</summary>
        Realm,

        /// <summary>Client roles only (<c>resource_access.&lt;client&gt;.roles</c> and <c>client_roles</c>).</summary>
        Client,

        /// <summary>Both realm and client roles (default).</summary>
        Both,
    }

    /// <summary>
    /// Flattens Keycloak's nested token claims into the flat claim shape ASP.NET Core
    /// authorization understands.
    /// <para>
    /// PERMANENT core: this is identical under Option 1 (JWT bearer) and Option 2 (BFF cookie),
    /// so it must not take on any assumption about how the principal was obtained.
    /// </para>
    /// <para>
    /// Keycloak nests roles inside JSON object claims (<c>realm_access</c> /
    /// <c>resource_access</c>), which <c>RequireRole</c> cannot see. This lifts every role
    /// into an individual <see cref="RolesClaimType"/> claim.
    /// </para>
    /// </summary>
    public static class KeycloakClaims
    {
        /// <summary>The flat claim type each role is written to (matches RoleClaimType).</summary>
        public const string RolesClaimType = "roles";

        /// <summary>Canonical claim type for the Basic BCeID user GUID.</summary>
        public const string BceidGuidClaimType = "bceid_user_guid";

        /// <summary>Canonical claim type for the IDIR username.</summary>
        public const string IdirUsernameClaimType = "idir_username";

        /// <summary>Alternate spelling of the BCeID GUID seen on some BC Gov realms.</summary>
        private const string BceidGuidAltClaimType = "bceid_guid";

        /// <summary>Claim type BC Gov's client-roles mapper emits (already flattened).</summary>
        private const string ClientRolesClaimType = "client_roles";

        private const string RealmAccessClaimType = "realm_access";
        private const string ResourceAccessClaimType = "resource_access";
        private const string RolesJsonProperty = "roles";

        /// <summary>
        /// Flattens roles and keystone identifiers into <paramref name="principal"/> in place.
        /// Safe to call more than once: existing claims are never duplicated.
        /// </summary>
        /// <param name="principal">The principal built from the validated token.</param>
        /// <param name="clientId">
        /// The client whose roles to read from <c>resource_access</c>. When null, roles from
        /// every client in the token are taken.
        /// </param>
        /// <param name="source">Which role location(s) to read. Defaults to both.</param>
        public static void MapInto(
            ClaimsPrincipal principal,
            string? clientId = null,
            KeycloakRoleSource source = KeycloakRoleSource.Both)
        {
            ArgumentNullException.ThrowIfNull(principal);

            if (principal.Identity is not ClaimsIdentity identity)
            {
                return;
            }

            foreach (string role in CollectRoles(principal, clientId, source))
            {
                // Don't re-add a role the token already carried in flat form.
                bool alreadyPresent = principal
                    .FindAll(RolesClaimType)
                    .Any(c => string.Equals(c.Value, role, StringComparison.Ordinal));

                if (!alreadyPresent)
                {
                    identity.AddClaim(new Claim(RolesClaimType, role));
                }
            }

            NormalizeBceidGuid(principal, identity);
        }

        private static HashSet<string> CollectRoles(
            ClaimsPrincipal principal,
            string? clientId,
            KeycloakRoleSource source)
        {
            var roles = new HashSet<string>(StringComparer.Ordinal);

            bool wantRealm = source is KeycloakRoleSource.Realm or KeycloakRoleSource.Both;
            bool wantClient = source is KeycloakRoleSource.Client or KeycloakRoleSource.Both;

            if (wantRealm)
            {
                AddRolesFromJsonObject(principal.FindFirst(RealmAccessClaimType)?.Value, roles);
            }

            if (wantClient)
            {
                // Already-flattened client roles (BC Gov client-roles mapper).
                foreach (Claim claim in principal.FindAll(ClientRolesClaimType))
                {
                    AddIfNotEmpty(claim.Value, roles);
                }

                AddRolesFromResourceAccess(
                    principal.FindFirst(ResourceAccessClaimType)?.Value, clientId, roles);
            }

            return roles;
        }

        /// <summary>Reads <c>{ "roles": [ ... ] }</c>.</summary>
        private static void AddRolesFromJsonObject(string? json, HashSet<string> roles)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                AddRolesFromElement(document.RootElement, roles);
            }
            catch (JsonException)
            {
                // A malformed claim must not break authentication; the user simply gets no
                // roles from this source and authorization fails closed.
            }
        }

        /// <summary>Reads <c>{ "&lt;client&gt;": { "roles": [ ... ] } }</c>.</summary>
        private static void AddRolesFromResourceAccess(
            string? json, string? clientId, HashSet<string> roles)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);

                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    if (document.RootElement.TryGetProperty(clientId, out JsonElement client))
                    {
                        AddRolesFromElement(client, roles);
                    }

                    return;
                }

                foreach (JsonProperty client in document.RootElement.EnumerateObject())
                {
                    AddRolesFromElement(client.Value, roles);
                }
            }
            catch (JsonException)
            {
                // See AddRolesFromJsonObject: fail closed rather than throw.
            }
        }

        private static void AddRolesFromElement(JsonElement element, HashSet<string> roles)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(RolesJsonProperty, out JsonElement rolesElement)
                || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement role in rolesElement.EnumerateArray())
            {
                if (role.ValueKind == JsonValueKind.String)
                {
                    AddIfNotEmpty(role.GetString(), roles);
                }
            }
        }

        private static void AddIfNotEmpty(string? value, HashSet<string> roles)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                roles.Add(value);
            }
        }

        /// <summary>
        /// Ensures the BCeID GUID is readable under one canonical claim type regardless of
        /// which spelling the realm emitted.
        /// </summary>
        private static void NormalizeBceidGuid(ClaimsPrincipal principal, ClaimsIdentity identity)
        {
            if (principal.FindFirst(BceidGuidClaimType) is not null)
            {
                return;
            }

            string? alternate = principal.FindFirst(BceidGuidAltClaimType)?.Value;
            if (!string.IsNullOrWhiteSpace(alternate))
            {
                identity.AddClaim(new Claim(BceidGuidClaimType, alternate));
            }
        }
    }
}
