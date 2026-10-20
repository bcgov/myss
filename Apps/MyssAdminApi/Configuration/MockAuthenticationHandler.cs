namespace Myss.AdminApi.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Security.Claims;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// A development persona: the claim set a real Keycloak token would have carried.
    /// </summary>
    /// <param name="Subject">The <c>sub</c> claim.</param>
    /// <param name="Roles">Roles granted to the persona.</param>
    /// <param name="BceidGuid">Basic BCeID GUID, for citizen personas.</param>
    /// <param name="IdirUsername">IDIR username, for staff personas.</param>
    /// <param name="IdentityProvider">The <c>identity_provider</c> claim, when the persona mirrors a brokered token.</param>
    public sealed record MockPersona(
        string Subject,
        string[] Roles,
        string? BceidGuid = null,
        string? IdirUsername = null,
        string? IdentityProvider = null);

    /// <summary>
    /// Signs every request in as a fixed development persona.
    /// <para>
    /// Only ever registered when <see cref="MockAuthGate.Evaluate"/> returns true, so it cannot
    /// reach production. Selecting a persona per request (header <c>X-Mock-Persona</c>) keeps
    /// role-based behaviour testable without restarting the API.
    /// </para>
    /// </summary>
    public class MockAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        /// <summary>The authentication scheme name.</summary>
        public const string SchemeName = "MockAuth";

        /// <summary>Request header used to pick a persona for a single request.</summary>
        public const string PersonaHeader = "X-Mock-Persona";

        /// <summary>The personas available to developers and tests.</summary>
        public static readonly IReadOnlyDictionary<string, MockPersona> Personas =
            new Dictionary<string, MockPersona>(StringComparer.OrdinalIgnoreCase)
            {
                ["admin"] = new("mock-admin", [MyssRoles.Admin], IdirUsername: "MADMIN"),
                ["worker"] = new("mock-worker", [MyssRoles.Worker], IdirUsername: "MWORKER"),

                // Deliberately has the WORKER role but no IDIR identity, so the
                // AdminIdir policy can be shown to reject it.
                ["workernoidir"] = new("mock-worker-no-idir", [MyssRoles.Worker]),

                // A signed-in citizen, which the AdminIdir policy refuses.
                ["alice"] = new("mock-alice", [MyssRoles.Client], BceidGuid: "11111111-1111-1111-1111-111111111111"),
            };

        private readonly string defaultPersona;

        /// <summary>Initializes a new instance of the <see cref="MockAuthenticationHandler"/> class.</summary>
        /// <param name="options">The scheme options monitor.</param>
        /// <param name="logger">The logger factory.</param>
        /// <param name="encoder">The URL encoder.</param>
        /// <param name="configuration">The application configuration (selects the default persona).</param>
        public MockAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            Microsoft.Extensions.Configuration.IConfiguration configuration)
            : base(options, logger, encoder)
        {
            this.defaultPersona = configuration[MockAuthGate.PersonaKey] ?? "admin";
        }

        /// <inheritdoc/>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string requested = this.Request.Headers.TryGetValue(PersonaHeader, out var header)
                && !string.IsNullOrWhiteSpace(header.ToString())
                    ? header.ToString()
                    : this.defaultPersona;

            if (!Personas.TryGetValue(requested, out MockPersona? persona))
            {
                return Task.FromResult(
                    AuthenticateResult.Fail($"Unknown mock persona '{requested}'."));
            }

            ClaimsPrincipal principal = BuildPrincipal(persona);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        /// <summary>
        /// Builds the principal for a persona, using the same claim types and role claim type as
        /// the real JWT pipeline so authorization behaves identically.
        /// </summary>
        /// <param name="persona">The persona to build.</param>
        /// <returns>The claims principal.</returns>
        public static ClaimsPrincipal BuildPrincipal(MockPersona persona)
        {
            ArgumentNullException.ThrowIfNull(persona);

            var claims = new List<Claim> { new("sub", persona.Subject) };

            foreach (string role in persona.Roles)
            {
                claims.Add(new Claim(KeycloakClaims.RolesClaimType, role));
            }

            if (!string.IsNullOrWhiteSpace(persona.BceidGuid))
            {
                claims.Add(new Claim(KeycloakClaims.BceidGuidClaimType, persona.BceidGuid));
            }

            if (!string.IsNullOrWhiteSpace(persona.IdirUsername))
            {
                claims.Add(new Claim(KeycloakClaims.IdirUsernameClaimType, persona.IdirUsername));
            }

            if (!string.IsNullOrWhiteSpace(persona.IdentityProvider))
            {
                claims.Add(new Claim(
                    KeycloakClaims.IdentityProviderClaimType, persona.IdentityProvider));
            }

            var identity = new ClaimsIdentity(
                claims,
                authenticationType: SchemeName,
                nameType: "sub",
                roleType: KeycloakClaims.RolesClaimType);

            return new ClaimsPrincipal(identity);
        }
    }
}
