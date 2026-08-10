namespace Myss.Api.Tests
{
    using System.Security.Claims;
    using Microsoft.AspNetCore.Http;
    using Myss.Api.Configuration;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Xunit;

    /// <summary>
    /// The typed caller must read identically whatever produced the principal (real token or
    /// mock persona), and must fail closed to anonymous when there is nothing to read.
    /// </summary>
    public class CurrentUserAccessorTests
    {
        private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
            new(new ClaimsIdentity(claims, "test", "sub", KeycloakClaims.RolesClaimType));

        [Fact]
        public void ReadsSubjectRolesAndKeystoneClaims()
        {
            var principal = Authenticated(
                new Claim("sub", "user-1"),
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Worker),
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Admin),
                new Claim(KeycloakClaims.BceidGuidClaimType, "guid-1"),
                new Claim(KeycloakClaims.IdirUsernameClaimType, "AJONES"));

            CurrentUser user = CurrentUserAccessor.FromPrincipal(principal);

            Assert.True(user.IsAuthenticated);
            Assert.Equal("user-1", user.Subject);
            Assert.Equal([MyssRoles.Worker, MyssRoles.Admin], user.Roles);
            Assert.Equal("guid-1", user.BceidGuid);
            Assert.Equal("AJONES", user.IdirUsername);
        }

        [Fact]
        public void UnauthenticatedPrincipalIsAnonymous()
        {
            CurrentUser user = CurrentUserAccessor.FromPrincipal(
                new ClaimsPrincipal(new ClaimsIdentity()));

            Assert.False(user.IsAuthenticated);
            Assert.Empty(user.Subject);
            Assert.Empty(user.Roles);
        }

        [Fact]
        public void NullPrincipalIsAnonymous()
        {
            Assert.Same(CurrentUser.Anonymous, CurrentUserAccessor.FromPrincipal(null));
        }

        [Fact]
        public void DeduplicatesRepeatedRoleClaims()
        {
            var principal = Authenticated(
                new Claim("sub", "user-1"),
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Client),
                new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Client));

            Assert.Single(CurrentUserAccessor.FromPrincipal(principal).Roles);
        }

        [Fact]
        public void FallsBackToNameIdentifierWhenSubIsAbsent()
        {
            var principal = Authenticated(new Claim(ClaimTypes.NameIdentifier, "legacy-id"));

            Assert.Equal("legacy-id", CurrentUserAccessor.FromPrincipal(principal).Subject);
        }

        [Fact]
        public void OptionalKeystoneClaimsAreNullWhenAbsent()
        {
            var principal = Authenticated(new Claim("sub", "user-1"));

            CurrentUser user = CurrentUserAccessor.FromPrincipal(principal);

            Assert.Null(user.BceidGuid);
            Assert.Null(user.IdirUsername);
        }

        [Fact]
        public void ReadsTheCallerFromTheHttpContext()
        {
            var accessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Authenticated(
                        new Claim("sub", "user-2"),
                        new Claim(KeycloakClaims.RolesClaimType, MyssRoles.Client)),
                },
            };

            CurrentUser user = new CurrentUserAccessor(accessor).User;

            Assert.True(user.IsAuthenticated);
            Assert.Equal("user-2", user.Subject);
            Assert.Equal([MyssRoles.Client], user.Roles);
        }

        [Fact]
        public void NoHttpContextIsAnonymous()
        {
            var accessor = new HttpContextAccessor { HttpContext = null };

            Assert.False(new CurrentUserAccessor(accessor).User.IsAuthenticated);
        }

        [Fact]
        public void MockPersonaProducesTheSameTypedCaller()
        {
            var principal = MockAuthenticationHandler.BuildPrincipal(
                MockAuthenticationHandler.Personas["alice"]);

            CurrentUser user = CurrentUserAccessor.FromPrincipal(principal);

            Assert.True(user.IsAuthenticated);
            Assert.Equal("mock-alice", user.Subject);
            Assert.Equal([MyssRoles.Client], user.Roles);
            Assert.NotNull(user.BceidGuid);
        }
    }
}
