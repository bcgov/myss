namespace Myss.Api.Tests
{
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.Extensions.DependencyInjection;
    using Myss.Api.Configuration;
    using Xunit;

    /// <summary>
    /// Policies are evaluated directly through <see cref="IAuthorizationService"/>, which is the
    /// same evaluation <c>[Authorize(Policy = ...)]</c> performs — without needing a live
    /// endpoint or a real token.
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
        public async Task AnonymousUserFailsEveryPolicy()
        {
            var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

            Assert.False((await Evaluate(anonymous, MyssPolicies.Client)).Succeeded);
            Assert.False((await Evaluate(anonymous, MyssPolicies.Worker)).Succeeded);
            Assert.False((await Evaluate(anonymous, MyssPolicies.Admin)).Succeeded);
            Assert.False((await Evaluate(anonymous, MyssPolicies.WorkerWithIdir)).Succeeded);
        }

        [Fact]
        public async Task ClientPolicyAcceptsClientRole()
        {
            Assert.True((await Evaluate(WithRoles(MyssRoles.Client), MyssPolicies.Client)).Succeeded);
        }

        [Fact]
        public async Task ClientPolicyRejectsWorker()
        {
            Assert.False((await Evaluate(WithRoles(MyssRoles.Worker), MyssPolicies.Client)).Succeeded);
        }

        [Fact]
        public async Task WorkerPolicyAcceptsWorker()
        {
            Assert.True((await Evaluate(WithRoles(MyssRoles.Worker), MyssPolicies.Worker)).Succeeded);
        }

        [Fact]
        public async Task WorkerPolicyIsAlsoSatisfiedByAdmin()
        {
            Assert.True((await Evaluate(WithRoles(MyssRoles.Admin), MyssPolicies.Worker)).Succeeded);
        }

        [Fact]
        public async Task WorkerPolicyRejectsClient()
        {
            Assert.False((await Evaluate(WithRoles(MyssRoles.Client), MyssPolicies.Worker)).Succeeded);
        }

        [Fact]
        public async Task AdminPolicyAcceptsOnlyAdmin()
        {
            Assert.True((await Evaluate(WithRoles(MyssRoles.Admin), MyssPolicies.Admin)).Succeeded);
            Assert.False((await Evaluate(WithRoles(MyssRoles.Worker), MyssPolicies.Admin)).Succeeded);
            Assert.False((await Evaluate(WithRoles(MyssRoles.Client), MyssPolicies.Admin)).Succeeded);
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
        public async Task RolesFlattenedFromARealTokenShapeSatisfyPolicies()
        {
            // End-to-end of the permanent core: nested Keycloak claim -> MapInto -> policy.
            var principal = User(new Claim("realm_access", """{"roles":["WORKER"]}"""));
            KeycloakClaims.MapInto(principal, "sdpr-my-ss-6498");

            Assert.True((await Evaluate(principal, MyssPolicies.Worker)).Succeeded);
        }

        [Theory]
        [InlineData("alice", MyssPolicies.Client, true)]
        [InlineData("alice", MyssPolicies.Worker, false)]
        [InlineData("worker", MyssPolicies.Worker, true)]
        [InlineData("worker", MyssPolicies.WorkerWithIdir, true)]
        [InlineData("workernoidir", MyssPolicies.Worker, true)]
        [InlineData("workernoidir", MyssPolicies.WorkerWithIdir, false)]
        [InlineData("admin", MyssPolicies.Worker, true)]
        [InlineData("admin", MyssPolicies.Admin, true)]
        public async Task MockPersonasAuthorizeAsExpected(string persona, string policy, bool expected)
        {
            var principal = MockAuthenticationHandler.BuildPrincipal(
                MockAuthenticationHandler.Personas[persona]);

            Assert.Equal(expected, (await Evaluate(principal, policy)).Succeeded);
        }
    }
}
