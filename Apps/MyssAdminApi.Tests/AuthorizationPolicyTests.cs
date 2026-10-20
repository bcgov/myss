namespace Myss.AdminApi.Tests
{
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Extensions.DependencyInjection;
    using Myss.AdminApi.Configuration;
    using Xunit;

    /// <summary>
    /// The AdminIdir policy evaluated directly through <see cref="IAuthorizationService"/>,
    /// which is the same evaluation <c>[Authorize(Policy = ...)]</c> performs — without
    /// needing a live endpoint or a real token.
    /// </summary>
    public class AuthorizationPolicyTests
    {
        private static readonly IAuthorizationService AuthorizationService = BuildService();

        private static IAuthorizationService BuildService()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMyssAuthorization();
            return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        private static ClaimsPrincipal User(params Claim[] claims) =>
            new(new ClaimsIdentity(claims, "test", "sub", KeycloakClaims.RolesClaimType));

        private static ClaimsPrincipal WithRoles(params string[] roles)
        {
            var claims = new Claim[roles.Length];
            for (int i = 0; i < roles.Length; i++)
            {
                claims[i] = new Claim(KeycloakClaims.RolesClaimType, roles[i]);
            }

            return User(claims);
        }

        private static Task<AuthorizationResult> Evaluate(ClaimsPrincipal user, string policy) =>
            AuthorizationService.AuthorizeAsync(user, resource: null, policy);

        [Fact]
        public async Task AdminIdirRejectsAnonymous()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

            Assert.False((await Evaluate(anonymous, MyssPolicies.AdminIdir)).Succeeded);
        }

        [Fact]
        public async Task AdminIdirAcceptsAnyIdirIdentity()
        {
            // The form editor gates on a government identity, not on a staff role:
            // an IDIR sign-in is enough, a BCeID citizen is not.
            var idir = User(new Claim(KeycloakClaims.IdirUsernameClaimType, "AJONES"));

            Assert.True((await Evaluate(idir, MyssPolicies.AdminIdir)).Succeeded);
        }

        [Fact]
        public async Task AdminIdirRejectsAnAuthenticatedCitizenWithoutIdir()
        {
            Assert.False((await Evaluate(WithRoles(MyssRoles.Client), MyssPolicies.AdminIdir)).Succeeded);
        }

        [Fact]
        public async Task AdminIdirRejectsAWorkerWithoutAnIdirIdentity()
        {
            Assert.False((await Evaluate(WithRoles(MyssRoles.Worker), MyssPolicies.AdminIdir)).Succeeded);
        }

        [Theory]
        [InlineData("alice", false)]
        [InlineData("worker", true)]
        [InlineData("admin", true)]
        [InlineData("workernoidir", false)]
        public async Task MockPersonasAuthorizeAsExpected(string persona, bool expected)
        {
            var principal = MockAuthenticationHandler.BuildPrincipal(
                MockAuthenticationHandler.Personas[persona]);

            Assert.Equal(expected, (await Evaluate(principal, MyssPolicies.AdminIdir)).Succeeded);
        }
    }
}
