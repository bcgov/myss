namespace Myss.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using Myss.Api.Configuration;
    using Xunit;

    /// <summary>
    /// The role calculator is the single place effective roles are computed (ADR-0007).
    /// The shared standard realm cannot know MySS account state, so on this portal the
    /// citizen role derives from the identity provider (RULE-IDA-08: Basic BCeID is the
    /// citizen path), while worker roles pass through from CSS-assigned client roles.
    /// </summary>
    public class RoleCalculatorTests
    {
        private const string ClientId = "sdpr-my-ss-6498";

        private static string[] Calculate(
            string? idp,
            string[]? tokenRoles = null,
            bool derive = true,
            bool businessGuid = false,
            string? bcServicesCardIdp = null)
        {
            var token = new TokenIdentity
            {
                IdentityProvider = idp,
                Roles = tokenRoles ?? [],
                HasBceidBusinessGuid = businessGuid,
            };

            return
            [
                .. RoleCalculator
                    .Calculate(token, MyssAccountSnapshot.Empty, derive, bcServicesCardIdp)
                    .Order(),
            ];
        }

        [Fact]
        public void BasicBceidSignInGrantsClient()
        {
            Assert.Equal([MyssRoles.Client], Calculate(IdentityProviders.BceidBasic));
        }

        [Fact]
        public void BcServicesCardSignInGrantsClient()
        {
            Assert.Equal([MyssRoles.Client], Calculate(IdentityProviders.BcServicesCard));
        }

        [Fact]
        public void BceidBothSignInWithoutBusinessGuidGrantsClient()
        {
            // The dev integration enables bceidboth, not bceidbasic (token seen 2026-09-09).
            Assert.Equal([MyssRoles.Client], Calculate(IdentityProviders.BceidBoth));
        }

        [Fact]
        public void BceidBothSignInWithBusinessGuidGetsNoCitizenRole()
        {
            // RULE-IDA-08: the same broker also admits Business BCeID; its GUID marks it.
            Assert.Empty(Calculate(IdentityProviders.BceidBoth, businessGuid: true));
        }

        [Fact]
        public void ConfiguredBcServicesCardAliasGrantsClient()
        {
            // CSS brokers BC Services Card per integration under the client id.
            Assert.Equal(
                [MyssRoles.Client],
                Calculate(ClientId, bcServicesCardIdp: ClientId));
        }

        [Fact]
        public void ConfiguredBcServicesCardAliasStripsWorkerRoles()
        {
            Assert.Equal(
                [MyssRoles.Client],
                Calculate(ClientId, [MyssRoles.Worker], bcServicesCardIdp: ClientId));
        }

        [Fact]
        public void ClientIdIsNotACitizenIdpWhenNoBcServicesCardAliasIsConfigured()
        {
            Assert.Empty(Calculate(ClientId));
        }

        [Fact]
        public void BusinessBceidGetsNoCitizenRole()
        {
            // RULE-IDA-08: Business BCeID is not a citizen sign-in path. Its tokens also
            // carry bceid_user_guid, which is exactly why derivation keys on the identity
            // provider and never on GUID presence.
            Assert.Empty(Calculate(IdentityProviders.BceidBusiness));
        }

        [Fact]
        public void IdirTokenIsNotACitizen()
        {
            Assert.Equal(
                [MyssRoles.Worker],
                Calculate(IdentityProviders.Idir, [MyssRoles.Worker]));
        }

        [Fact]
        public void CitizenTokenNeverCarriesWorkerRoles()
        {
            // A CSS mis-assignment must not hand a citizen sign-in worker power.
            Assert.Equal(
                [MyssRoles.Client],
                Calculate(IdentityProviders.BceidBasic, [MyssRoles.Worker, MyssRoles.Admin]));
        }

        [Fact]
        public void WorkerTokenNeverCarriesCitizenRoles()
        {
            // The complement: IDIR is not accepted on the client path (RULE-IDA-08).
            Assert.Equal(
                [MyssRoles.Worker],
                Calculate(IdentityProviders.Idir, [MyssRoles.Worker, MyssRoles.Client]));
        }

        [Fact]
        public void AzureIdirCountsAsAWorkerIdp()
        {
            Assert.Equal(
                [MyssRoles.Admin],
                Calculate(IdentityProviders.AzureIdir, [MyssRoles.Admin, MyssRoles.Client]));
        }

        [Fact]
        public void MissingIdpPassesRolesThroughUnchanged()
        {
            // Mock personas carry explicit roles and no identity_provider claim; the
            // calculator must not disturb them.
            Assert.Equal([MyssRoles.Client], Calculate(idp: null, [MyssRoles.Client]));
        }

        [Fact]
        public void UnknownIdpPassesRolesThroughWithoutDerivation()
        {
            Assert.Equal(["SOMETHING"], Calculate("github", ["SOMETHING"]));
        }

        [Fact]
        public void IdpComparisonIsCaseInsensitive()
        {
            Assert.Equal([MyssRoles.Client], Calculate("BceidBasic"));
        }

        [Fact]
        public void DerivationSwitchOffAddsNothing()
        {
            Assert.Empty(Calculate(IdentityProviders.BceidBasic, derive: false));
        }

        [Fact]
        public void DerivationSwitchOffStillStripsWorkerRolesFromCitizenTokens()
        {
            // Stripping is hardening, not derivation; it applies regardless of the switch.
            Assert.Empty(
                Calculate(IdentityProviders.BceidBasic, [MyssRoles.Worker], derive: false));
        }

        [Fact]
        public void TokenIdentityReadsIdpAndFlattenedRolesFromThePrincipal()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                new List<Claim>
                {
                    new("sub", "abc"),
                    new(KeycloakClaims.IdentityProviderClaimType, IdentityProviders.BceidBasic),
                    new(KeycloakClaims.RolesClaimType, MyssRoles.Worker),
                    new(KeycloakClaims.RolesClaimType, MyssRoles.Worker),
                    new(KeycloakClaims.BceidBusinessGuidClaimType, "ABC123"),
                },
                "test",
                "sub",
                KeycloakClaims.RolesClaimType));

            TokenIdentity token = TokenIdentity.FromPrincipal(principal);

            Assert.Equal(IdentityProviders.BceidBasic, token.IdentityProvider);
            Assert.Equal([MyssRoles.Worker], token.Roles.Distinct().ToArray());
            Assert.True(token.HasBceidBusinessGuid);
        }
    }
}
