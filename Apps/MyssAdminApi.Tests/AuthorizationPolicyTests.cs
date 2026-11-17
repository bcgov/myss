namespace Myss.AdminApi.Tests
{
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Extensions.DependencyInjection;
    using Myss.AdminApi.Configuration;
    using Xunit;
    using Myss.Common.Auth;

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

        [Fact]
        public async Task WorkerWithIdirRequiresBothTheRoleAndTheIdirClaim()
        {
            var withIdir = User(
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Worker),
                new Claim(KeycloakClaims.IdirUsernameClaimType, "AJONES"));

            Assert.True((await Evaluate(withIdir, MyssPolicies.WorkerWithIdir)).Succeeded);
        }

        [Fact]
        public async Task WorkerWithIdirRejectsAWorkerWithoutAnIdirIdentity()
        {
            // The hardening control: role alone is not enough.
            Assert.False(
                (await Evaluate(WithRoles(MyssRoles.Worker), MyssPolicies.WorkerWithIdir)).Succeeded);
        }

        [Fact]
        public async Task WorkerWithIdirRejectsAClientEvenWithAnIdirClaim()
        {
            var clientWithIdir = User(
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Client),
                new Claim(KeycloakClaims.IdirUsernameClaimType, "AJONES"));

            Assert.False((await Evaluate(clientWithIdir, MyssPolicies.WorkerWithIdir)).Succeeded);
        }

        [Fact]
        public async Task WorkerWithIdirRejectsAnonymous()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

            Assert.False((await Evaluate(anonymous, MyssPolicies.WorkerWithIdir)).Succeeded);
        }

        [Fact]
        public async Task WorkerWithIdirRejectsAnIdirUserWithoutAStaffRole()
        {
            // AdminIdir lets any IDIR user into the form editor; the worker endpoints
            // hold citizens' answers and also need WORKER or ADMIN.
            var idirOnly = User(new Claim(KeycloakClaims.IdirUsernameClaimType, "AJONES"));

            Assert.True((await Evaluate(idirOnly, MyssPolicies.AdminIdir)).Succeeded);
            Assert.False((await Evaluate(idirOnly, MyssPolicies.WorkerWithIdir)).Succeeded);
        }

        [Fact]
        public async Task RolesFlattenedFromARealTokenShapeSatisfyWorkerWithIdir()
        {
            // Nested Keycloak claim -> MapInto -> policy, as in MyssApi.
            var principal = User(
                new Claim("realm_access", """{"roles":["WORKER"]}"""),
                new Claim(KeycloakClaims.IdirUsernameClaimType, "AJONES"));
            KeycloakClaims.MapInto(principal, "sdpr-my-ss-6498");

            Assert.True((await Evaluate(principal, MyssPolicies.WorkerWithIdir)).Succeeded);
        }

        [Theory]
        [InlineData("alice", false)]
        [InlineData("worker", true)]
        [InlineData("admin", true)]
        [InlineData("workernoidir", false)]
        public async Task MockPersonasAuthorizeWorkerWithIdirAsExpected(string persona, bool expected)
        {
            var principal = MockAuthenticationHandler.BuildPrincipal(
                MockAuthenticationHandler.Personas[persona]);

            Assert.Equal(expected, (await Evaluate(principal, MyssPolicies.WorkerWithIdir)).Succeeded);
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
