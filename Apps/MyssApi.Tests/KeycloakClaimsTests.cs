namespace Myss.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using Myss.Api.Configuration;
    using Xunit;

    /// <summary>
    /// Keycloak nests roles inside JSON object claims that ASP.NET Core authorization cannot
    /// see. These cover the flattening in both role models, because which one this realm uses
    /// is still unconfirmed with IDIM.
    /// </summary>
    public class KeycloakClaimsTests
    {
        private const string ClientId = "sdpr-my-ss-6498";

        private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
            new(new ClaimsIdentity(claims, "test", "sub", KeycloakClaims.RolesClaimType));

        private static string[] RolesOf(ClaimsPrincipal principal) =>
            principal.FindAll(KeycloakClaims.RolesClaimType).Select(c => c.Value).Order().ToArray();

        [Fact]
        public void FlattensRealmRoles()
        {
            var principal = PrincipalWith(
                new Claim("realm_access", """{"roles":["CLIENT","WORKER"]}"""));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Equal(["CLIENT", "WORKER"], RolesOf(principal));
        }

        [Fact]
        public void FlattensClientRolesForTheConfiguredClient()
        {
            var principal = PrincipalWith(
                new Claim(
                    "resource_access",
                    """{"sdpr-my-ss-6498":{"roles":["ADMIN"]},"other-app":{"roles":["NOPE"]}}"""));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Equal(["ADMIN"], RolesOf(principal));
        }

        [Fact]
        public void ReadsAlreadyFlattenedClientRolesClaim()
        {
            var principal = PrincipalWith(new Claim("client_roles", "WORKER"));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Equal(["WORKER"], RolesOf(principal));
        }

        [Fact]
        public void MergesBothSourcesWithoutDuplicates()
        {
            var principal = PrincipalWith(
                new Claim("realm_access", """{"roles":["CLIENT","WORKER"]}"""),
                new Claim("resource_access", """{"sdpr-my-ss-6498":{"roles":["WORKER","ADMIN"]}}"""));

            KeycloakClaims.MapInto(principal, ClientId, KeycloakRoleSource.Both);

            Assert.Equal(["ADMIN", "CLIENT", "WORKER"], RolesOf(principal));
        }

        [Fact]
        public void RoleSourceRealmIgnoresClientRoles()
        {
            var principal = PrincipalWith(
                new Claim("realm_access", """{"roles":["CLIENT"]}"""),
                new Claim("resource_access", """{"sdpr-my-ss-6498":{"roles":["ADMIN"]}}"""));

            KeycloakClaims.MapInto(principal, ClientId, KeycloakRoleSource.Realm);

            Assert.Equal(["CLIENT"], RolesOf(principal));
        }

        [Fact]
        public void RoleSourceClientIgnoresRealmRoles()
        {
            var principal = PrincipalWith(
                new Claim("realm_access", """{"roles":["CLIENT"]}"""),
                new Claim("resource_access", """{"sdpr-my-ss-6498":{"roles":["ADMIN"]}}"""));

            KeycloakClaims.MapInto(principal, ClientId, KeycloakRoleSource.Client);

            Assert.Equal(["ADMIN"], RolesOf(principal));
        }

        [Fact]
        public void IsIdempotentAndDoesNotDuplicateExistingRoleClaims()
        {
            var principal = PrincipalWith(
                new Claim(KeycloakClaims.RolesClaimType, "CLIENT"),
                new Claim("realm_access", """{"roles":["CLIENT"]}"""));

            KeycloakClaims.MapInto(principal, ClientId);
            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Single(principal.FindAll(KeycloakClaims.RolesClaimType));
        }

        [Fact]
        public void NormalizesTheAlternateBceidGuidSpelling()
        {
            var principal = PrincipalWith(new Claim("bceid_guid", "guid-123"));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Equal(
                "guid-123",
                principal.FindFirst(KeycloakClaims.BceidGuidClaimType)?.Value);
        }

        [Fact]
        public void KeepsTheCanonicalBceidGuidWhenAlreadyPresent()
        {
            var principal = PrincipalWith(
                new Claim(KeycloakClaims.BceidGuidClaimType, "canonical"),
                new Claim("bceid_guid", "alternate"));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Single(principal.FindAll(KeycloakClaims.BceidGuidClaimType));
            Assert.Equal(
                "canonical",
                principal.FindFirst(KeycloakClaims.BceidGuidClaimType)?.Value);
        }

        [Fact]
        public void MalformedJsonFailsClosedRatherThanThrowing()
        {
            var principal = PrincipalWith(new Claim("realm_access", "not json at all"));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Empty(principal.FindAll(KeycloakClaims.RolesClaimType));
        }

        [Fact]
        public void TokenWithNoRoleClaimsYieldsNoRoles()
        {
            var principal = PrincipalWith(new Claim("sub", "abc"));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Empty(principal.FindAll(KeycloakClaims.RolesClaimType));
        }

        [Fact]
        public void FlattenedRolesAreVisibleToIsInRole()
        {
            var principal = PrincipalWith(
                new Claim("realm_access", """{"roles":["WORKER"]}"""));

            KeycloakClaims.MapInto(principal, ClientId);

            // The role claim type must line up with what RequireRole/[Authorize] reads.
            Assert.True(principal.IsInRole("WORKER"));
        }

        [Fact]
        public void NullClientIdTakesRolesFromEveryClient()
        {
            var principal = PrincipalWith(
                new Claim(
                    "resource_access",
                    """{"app-one":{"roles":["ONE"]},"app-two":{"roles":["TWO"]}}"""));

            KeycloakClaims.MapInto(principal, clientId: null);

            Assert.Equal(["ONE", "TWO"], RolesOf(principal));
        }

        [Fact]
        public void IgnoresAnIdentityLessPrincipalWithoutThrowing()
        {
            var principal = new ClaimsPrincipal();

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Empty(principal.FindAll(KeycloakClaims.RolesClaimType));
        }

        [Fact]
        public void PreservesIdirUsername()
        {
            var principal = PrincipalWith(
                new Claim(KeycloakClaims.IdirUsernameClaimType, "AJONES"));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Equal(
                "AJONES",
                principal.FindFirst(KeycloakClaims.IdirUsernameClaimType)?.Value);
        }

        [Fact]
        public void DoesNotInventRolesFromAnUnrelatedClient()
        {
            var principal = PrincipalWith(
                new Claim("resource_access", """{"other-app":{"roles":["ADMIN"]}}"""));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Empty(principal.FindAll(KeycloakClaims.RolesClaimType));
        }

        [Fact]
        public void HandlesRealmAccessWithoutARolesArray()
        {
            var principal = PrincipalWith(new Claim("realm_access", """{"other":"value"}"""));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Empty(principal.FindAll(KeycloakClaims.RolesClaimType));
        }

        [Fact]
        public void SkipsNonStringRoleEntries()
        {
            var principal = PrincipalWith(
                new Claim("realm_access", """{"roles":["CLIENT",42,null]}"""));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Equal(["CLIENT"], RolesOf(principal));
        }

        [Fact]
        public void MultipleFlattenedClientRoleClaimsAreAllRead()
        {
            var principal = PrincipalWith(
                new Claim("client_roles", "CLIENT"),
                new Claim("client_roles", "WORKER"));

            KeycloakClaims.MapInto(principal, ClientId);

            Assert.Equal(["CLIENT", "WORKER"], RolesOf(principal));
        }

        [Fact]
        public void AllPersonaRolesSurviveTheSameMappingContract()
        {
            // The mock handler must produce principals shaped exactly like real ones.
            var principal = MockAuthenticationHandler.BuildPrincipal(
                MockAuthenticationHandler.Personas["worker"]);

            Assert.True(principal.IsInRole(MyssRoles.Worker));
            Assert.Equal("MWORKER", principal.FindFirst(KeycloakClaims.IdirUsernameClaimType)?.Value);
        }

        [Theory]
        [InlineData("alice", MyssRoles.Client)]
        [InlineData("bob", MyssRoles.Client)]
        [InlineData("carol", MyssRoles.Client)]
        [InlineData("worker", MyssRoles.Worker)]
        [InlineData("admin", MyssRoles.Admin)]
        public void EveryPersonaCarriesItsExpectedRole(string persona, string expectedRole)
        {
            var principal = MockAuthenticationHandler.BuildPrincipal(
                MockAuthenticationHandler.Personas[persona]);

            Assert.True(principal.IsInRole(expectedRole));
            Assert.NotEmpty(principal.FindFirst("sub")!.Value);
        }

        [Fact]
        public void PersonasAreDistinctSubjects()
        {
            var subjects = new HashSet<string>();
            foreach (KeyValuePair<string, MockPersona> entry in MockAuthenticationHandler.Personas)
            {
                Assert.True(
                    subjects.Add(entry.Value.Subject),
                    $"Persona '{entry.Key}' reuses subject '{entry.Value.Subject}'.");
            }
        }
    }
}
