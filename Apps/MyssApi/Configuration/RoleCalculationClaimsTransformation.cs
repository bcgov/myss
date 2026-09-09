namespace Myss.Api.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// The adapter between the pure <see cref="RoleCalculator"/> and the ASP.NET Core
    /// machinery: the calculator <i>decides</i> the roles, this transformation
    /// <i>publishes</i> the decision into the principal as claims.
    /// <para>
    /// It exists because of who actually reads roles at runtime: nothing in the framework
    /// ever calls the calculator. Authorization (<c>RequireRole</c>) and
    /// <see cref="Services.CurrentUserAccessor"/> (and therefore <c>GET /v1/auth/me</c>)
    /// read only the flat role claims on the <see cref="ClaimsPrincipal"/> — so unless the
    /// verdict is written back as claims, it is invisible.
    /// <see cref="IClaimsTransformation"/> is the framework's hook for exactly that: it
    /// runs after authentication succeeds and before authorization evaluates.
    /// </para>
    /// <para>
    /// Why here and not the two obvious alternatives: <c>OnTokenValidated</c> belongs to
    /// the JWT scheme only (mock personas and Option 2's cookie scheme would miss it),
    /// while putting derivation inside the policies would make authorization enforce
    /// different roles than <c>/me</c> reports. Claims transformation runs after
    /// <b>every</b> scheme — real JWT, mock auth, the future BFF cookie — so every
    /// downstream reader sees one truth. PERMANENT core for that reason.
    /// </para>
    /// <para>
    /// It CONVERGES the flat role claims to the calculated set (removes claims outside
    /// the verdict — that is where a mis-assigned worker role on a citizen token actually
    /// disappears — and adds the missing ones) rather than appending, because ASP.NET
    /// Core may invoke a transformation more than once per request: it must be
    /// idempotent. This is also the deliberate impurity boundary — configuration and
    /// <see cref="ClaimsIdentity"/> mutation live here so the calculator stays pure.
    /// Today the account input is <see cref="MyssAccountSnapshot.Empty"/>; when the Identity
    /// domain lands, this is the seam that fetches the (session-cached) snapshot.
    /// </para>
    /// </summary>
    public sealed class RoleCalculationClaimsTransformation : IClaimsTransformation
    {
        /// <summary>
        /// Configuration switch for IDP-derived citizen roles (default true). Off is the
        /// escape hatch if IDIM ever mandates CSS-managed citizen roles (ADR-0007).
        /// </summary>
        public const string DeriveSwitchKey = "Oidc:DeriveClientRoleFromIdp";

        private readonly bool deriveCitizenRoleFromIdp;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoleCalculationClaimsTransformation"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public RoleCalculationClaimsTransformation(IConfiguration configuration)
        {
            this.deriveCitizenRoleFromIdp =
                configuration.GetValue<bool?>(DeriveSwitchKey) ?? true;
        }

        /// <inheritdoc/>
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            {
                return Task.FromResult(principal);
            }

            IReadOnlySet<string> effective = RoleCalculator.Calculate(
                TokenIdentity.FromPrincipal(principal),
                MyssAccountSnapshot.Empty,
                this.deriveCitizenRoleFromIdp);

            foreach (Claim stale in identity
                .FindAll(KeycloakClaims.RolesClaimType)
                .Where(c => !effective.Contains(c.Value))
                .ToList())
            {
                identity.TryRemoveClaim(stale);
            }

            foreach (string role in effective)
            {
                bool alreadyPresent = identity
                    .FindAll(KeycloakClaims.RolesClaimType)
                    .Any(c => string.Equals(c.Value, role, StringComparison.Ordinal));

                if (!alreadyPresent)
                {
                    identity.AddClaim(new Claim(KeycloakClaims.RolesClaimType, role));
                }
            }

            return Task.FromResult(principal);
        }
    }
}
