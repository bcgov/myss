namespace Myss.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Myss.Api.Configuration;
    using Xunit;

    /// <summary>
    /// The claims transformation feeds <see cref="RoleCalculator"/>'s verdict back into the
    /// principal so <c>RequireRole</c> policies and <c>CurrentUserAccessor</c> see effective
    /// roles without knowing they were computed. ASP.NET Core may invoke a transformation
    /// more than once per request, so idempotency is part of the contract.
    /// </summary>
    public class RoleCalculationClaimsTransformationTests
    {
        private static RoleCalculationClaimsTransformation Transformation(
            params KeyValuePair<string, string?>[] settings) =>
            new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

        private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
            new(new ClaimsIdentity(claims, "test", "sub", KeycloakClaims.RolesClaimType));

        private static string[] RolesOf(ClaimsPrincipal principal) =>
            principal.FindAll(KeycloakClaims.RolesClaimType).Select(c => c.Value).Order().ToArray();

        [Fact]
        public async Task GrantsClientToABasicBceidPrincipal()
        {
            var principal = Authenticated(
                new Claim("sub", "abc"),
                new Claim(KeycloakClaims.IdentityProviderClaimType, IdentityProviders.BceidBasic));

            await Transformation().TransformAsync(principal);

            Assert.Equal([MyssRoles.Client], RolesOf(principal));
            Assert.True(principal.IsInRole(MyssRoles.Client));
        }

        [Fact]
        public async Task RemovesWorkerRolesFromACitizenPrincipal()
        {
            var principal = Authenticated(
                new Claim(KeycloakClaims.IdentityProviderClaimType, IdentityProviders.BceidBasic),
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Worker),
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Admin));

            await Transformation().TransformAsync(principal);

            Assert.Equal([MyssRoles.Client], RolesOf(principal));
        }

        [Fact]
        public async Task IsIdempotentAcrossRepeatedInvocations()
        {
            var principal = Authenticated(
                new Claim(KeycloakClaims.IdentityProviderClaimType, IdentityProviders.BceidBasic));
            RoleCalculationClaimsTransformation transformation = Transformation();

            await transformation.TransformAsync(principal);
            await transformation.TransformAsync(principal);

            Assert.Equal([MyssRoles.Client], RolesOf(principal));
        }

        [Fact]
        public async Task LeavesAnUnauthenticatedPrincipalUntouched()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity());

            ClaimsPrincipal result = await Transformation().TransformAsync(principal);

            Assert.Same(principal, result);
            Assert.Empty(RolesOf(result));
        }

        [Fact]
        public async Task LeavesMockPersonaShapedPrincipalsUntouched()
        {
            // Mock personas carry explicit roles and no identity_provider claim.
            var principal = Authenticated(
                new Claim("sub", "mock-alice"),
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Client));

            await Transformation().TransformAsync(principal);

            Assert.Equal([MyssRoles.Client], RolesOf(principal));
        }

        [Fact]
        public async Task HonoursTheDeriveSwitch()
        {
            var principal = Authenticated(
                new Claim(KeycloakClaims.IdentityProviderClaimType, IdentityProviders.BceidBasic));

            await Transformation(
                    new KeyValuePair<string, string?>(
                        RoleCalculationClaimsTransformation.DeriveSwitchKey, "false"))
                .TransformAsync(principal);

            Assert.Empty(RolesOf(principal));
        }
    }
}
