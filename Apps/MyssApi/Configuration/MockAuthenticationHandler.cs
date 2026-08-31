namespace Myss.Api.Configuration
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
                ["alice"] = new("mock-alice", [MyssRoles.Client], BceidGuid: "11111111-1111-1111-1111-111111111111"),
                ["bob"] = new("mock-bob", [MyssRoles.Client], BceidGuid: "22222222-2222-2222-2222-222222222222"),
                ["carol"] = new("mock-carol", [MyssRoles.Client], BceidGuid: "33333333-3333-3333-3333-333333333333"),
                ["worker"] = new("mock-worker", [MyssRoles.Worker], IdirUsername: "MWORKER"),
                ["admin"] = new("mock-admin", [MyssRoles.Admin], IdirUsername: "MADMIN"),

                // Deliberately has the WORKER role but no IDIR identity, so the
                // WorkerWithIdir policy can be shown to reject it.
                ["workernoidir"] = new("mock-worker-no-idir", [MyssRoles.Worker]),

                // Mirrors a REAL Basic BCeID token: an identity_provider claim and NO
                // roles — CLIENT comes from RoleCalculator (ADR-0007), not the token.
                // alice/bob/carol predate the calculator and carry CLIENT explicitly.
                ["bceid"] = new(
                    "mock-bceid",
                    [],
                    BceidGuid: "44444444-4444-4444-4444-444444444444",
                    IdentityProvider: IdentityProviders.BceidBasic),
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
            this.defaultPersona = configuration[MockAuthGate.PersonaKey] ?? "alice";
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
