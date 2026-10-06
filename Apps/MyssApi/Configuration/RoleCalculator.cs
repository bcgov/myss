namespace Myss.Api.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;

    /// <summary>
    /// Identity-provider broker aliases the BC Gov standard realm mints into the
    /// <c>identity_provider</c> claim. The exact aliases are IDIM's; a rename shows up as
    /// citizens losing the CLIENT role, which the RoleCalculatorTests pin.
    /// </summary>
    public static class IdentityProviders
    {
        /// <summary>Basic BCeID — the citizen sign-in path (RULE-IDA-08).</summary>
        public const string BceidBasic = "bceidbasic";

        /// <summary>Business BCeID — never a citizen path (RULE-IDA-08).</summary>
        public const string BceidBusiness = "bceidbusiness";

        /// <summary>
        /// Basic and Business BCeID behind one broker — what the CSS integration actually
        /// enables (dev realm, 2026-09-09). A citizen only when the token carries no
        /// Business BCeID GUID (RULE-IDA-08).
        /// </summary>
        public const string BceidBoth = "bceidboth";

        /// <summary>
        /// BC Services Card under a fixed alias. CSS brokers BC Services Card per
        /// integration under the client id instead, so the live alias is configured
        /// (<c>Oidc:BcServicesCardIdp</c>, default the client id); this one stays accepted.
        /// </summary>
        public const string BcServicesCard = "bcservicescard";

        /// <summary>IDIR via SiteMinder — the worker sign-in path.</summary>
        public const string Idir = "idir";

        /// <summary>IDIR via Azure AD — same worker population as <see cref="Idir"/>.</summary>
        public const string AzureIdir = "azureidir";
    }

    /// <summary>
    /// What the token honestly attests about the caller: who authenticated them and which
    /// roles the realm delivered. Never carries anything derived — that is the calculator's
    /// output, not its input.
    /// </summary>
    public sealed record TokenIdentity
    {
        /// <summary>Gets the identity-provider alias, or null when the token carries none (mock personas).</summary>
        public string? IdentityProvider { get; init; }

        /// <summary>Gets the roles delivered on the token, flattened by <see cref="KeycloakClaims"/>.</summary>
        public IReadOnlyCollection<string> Roles { get; init; } = [];

        /// <summary>
        /// Gets a value indicating whether the token carries a Business BCeID GUID, which
        /// marks a <see cref="IdentityProviders.BceidBoth"/> sign-in as Business BCeID.
        /// </summary>
        public bool HasBceidBusinessGuid { get; init; }

        /// <summary>Reads the token identity off a validated principal.</summary>
        /// <param name="principal">The principal built from the validated token.</param>
        /// <returns>The typed token identity.</returns>
        public static TokenIdentity FromPrincipal(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            return new TokenIdentity
            {
                IdentityProvider = principal
                    .FindFirst(KeycloakClaims.IdentityProviderClaimType)?.Value,
                Roles = principal
                    .FindAll(KeycloakClaims.RolesClaimType)
                    .Select(c => c.Value)
                    .ToArray(),
                HasBceidBusinessGuid = !string.IsNullOrWhiteSpace(
                    principal.FindFirst(KeycloakClaims.BceidBusinessGuidClaimType)?.Value),
            };
        }
    }

    /// <summary>
    /// The MySS-owned account state a role can depend on. Empty today: RULE-WKR-04's
    /// APPLICANT/CLIENT split keys on facts only MySS knows (APPLICANT record, PROFILE,
    /// promoted ICM case), and the Identity domain that owns them has not landed yet.
    /// Those facts become properties here — and new rows in the calculator — when it does.
    /// </summary>
    public sealed record MyssAccountSnapshot
    {
        /// <summary>Gets the snapshot for a caller with no known account state.</summary>
        public static MyssAccountSnapshot Empty { get; } = new();
    }

    /// <summary>
    /// THE single place effective roles are computed (ADR-0007). Pure: takes what the token
    /// attests plus what MySS knows about the account, returns the effective role set —
    /// never touches HttpContext, configuration, or storage, so the whole RULE-WKR-04
    /// matrix is table-testable. Nothing in the framework calls this directly:
    /// <see cref="RoleCalculationClaimsTransformation"/> runs it per request and publishes
    /// the verdict into the principal's claims, the only place policies and
    /// <see cref="Services.CurrentUserAccessor"/> look.
    /// <para>
    /// The shared standard realm assigns client roles per user by hand, which cannot work
    /// for ~100k self-registering citizens — so the citizen role derives from the identity
    /// provider (on this portal, per RULE-IDA-08, a citizen IDP <i>is</i> the CLIENT grant),
    /// while worker roles pass through from CSS-assigned token roles. Either way a token's
    /// roles never cross the citizen/worker line.
    /// </para>
    /// </summary>
    public static class RoleCalculator
    {
        private static readonly string[] CitizenIdps =
            [IdentityProviders.BceidBasic, IdentityProviders.BcServicesCard];

        private static readonly string[] WorkerIdps =
            [IdentityProviders.Idir, IdentityProviders.AzureIdir];

        private static readonly string[] WorkerRoles = [MyssRoles.Worker, MyssRoles.Admin];

        private static readonly string[] CitizenRoles = [MyssRoles.Client];

        /// <summary>Computes the effective roles for a caller.</summary>
        /// <param name="token">What the token attests.</param>
        /// <param name="account">What MySS knows about the account.</param>
        /// <param name="deriveCitizenRoleFromIdp">
        /// Grants CLIENT to citizen-IDP sign-ins (<c>Oidc:DeriveClientRoleFromIdp</c>,
        /// default true). The cross-line stripping below is hardening, not derivation, and
        /// applies regardless.
        /// </param>
        /// <param name="bcServicesCardIdp">
        /// The alias this integration's BC Services Card broker mints into
        /// <c>identity_provider</c> (CSS names it after the client id). Null or blank
        /// accepts only the fixed <see cref="IdentityProviders.BcServicesCard"/> alias.
        /// </param>
        /// <returns>The effective role set.</returns>
        public static IReadOnlySet<string> Calculate(
            TokenIdentity token,
            MyssAccountSnapshot account,
            bool deriveCitizenRoleFromIdp = true,
            string? bcServicesCardIdp = null)
        {
            ArgumentNullException.ThrowIfNull(token);
            ArgumentNullException.ThrowIfNull(account);

            var roles = new HashSet<string>(token.Roles, StringComparer.Ordinal);

            bool citizenIdp = MatchesAny(token.IdentityProvider, CitizenIdps)
                || (MatchesAny(token.IdentityProvider, [IdentityProviders.BceidBoth])
                    && !token.HasBceidBusinessGuid)
                || (!string.IsNullOrWhiteSpace(bcServicesCardIdp)
                    && MatchesAny(token.IdentityProvider, [bcServicesCardIdp]));
            bool workerIdp = MatchesAny(token.IdentityProvider, WorkerIdps);

            if (citizenIdp)
            {
                roles.ExceptWith(WorkerRoles);

                if (deriveCitizenRoleFromIdp)
                {
                    // When MyssAccountSnapshot grows the Identity-domain facts, this is where
                    // CLIENT splits into APPLICANT vs CLIENT per RULE-WKR-04.
                    roles.Add(MyssRoles.Client);
                }
            }

            if (workerIdp)
            {
                roles.ExceptWith(CitizenRoles);
            }

            // An unknown or absent IDP (mock personas, a future broker) passes through
            // untouched: fail toward the token, never toward a guess.
            return roles;
        }

        private static bool MatchesAny(string? identityProvider, string[] candidates) =>
            !string.IsNullOrWhiteSpace(identityProvider)
            && candidates.Any(c =>
                string.Equals(c, identityProvider.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
