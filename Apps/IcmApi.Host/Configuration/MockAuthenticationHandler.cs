namespace Icm.Api.Host.Configuration
{
    using System.Security.Claims;
    using System.Text.Encodings.Web;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Signs every request in as a fixed development caller.
    /// <para>
    /// Only ever registered when <see cref="MockAuthGate.Evaluate"/> returns true,
    /// so it cannot reach production. There is one persona because this host has
    /// one kind of caller: another service.
    /// </para>
    /// </summary>
    public class MockAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        /// <summary>The authentication scheme name.</summary>
        public const string SchemeName = "MockAuth";

        /// <summary>The <c>azp</c> the mock caller presents.</summary>
        public const string MockClientId = "mock-caller";

        /// <summary>Initializes a new instance of the <see cref="MockAuthenticationHandler"/> class.</summary>
        /// <param name="options">The scheme options monitor.</param>
        /// <param name="logger">The logger factory.</param>
        /// <param name="encoder">The URL encoder.</param>
        public MockAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        /// <inheritdoc/>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim("sub", MockClientId), new Claim("azp", MockClientId)],
                authenticationType: SchemeName,
                nameType: "sub",
                roleType: "roles");

            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
