namespace Icm.Api.Tests.Contracts
{
    using System.Reflection;
    using Icm.Api.Repositories;

    /// <summary>
    /// Guards the assembly's published surface.
    /// </summary>
    /// <remarks>
    /// The encapsulation this library relies on is one keyword per file, and a keyword is
    /// easy to get wrong — a wire model that quietly becomes <c>public</c> is a boundary
    /// gone, and nothing else would notice. This turns the rule into something that fails.
    /// </remarks>
    public class PublishedSurfaceTests
    {
        private static readonly Assembly Library = typeof(IServiceRequestRepository).Assembly;

        [Fact]
        public void OnlyModelsRepositoriesAndServicesAreExported()
        {
            string[] exported = [.. Library.GetExportedTypes()
                .Select(type => type.FullName!)
                .Order(StringComparer.Ordinal)];

            // Written out rather than pattern-matched, so widening the surface means saying
            // so here — which is the moment to ask whether it should be widened at all.
            string[] expected =
            [
                "Icm.Api.Models.AccessToken",
                "Icm.Api.Models.OAuthClientCredentials",
                "Icm.Api.Models.ServiceRequest",
                "Icm.Api.Models.ServiceRequestInput",
                "Icm.Api.Models.ServiceRequestLink",
                "Icm.Api.Models.ServiceRequestPage",
                "Icm.Api.Models.ServiceRequestQuery",
                "Icm.Api.Models.ServiceRequestReadOptions",
                "Icm.Api.Repositories.IOAuthTokenRepository",
                "Icm.Api.Repositories.IServiceRequestRepository",
                "Icm.Api.Repositories.IcmResponseException",
                "Icm.Api.Repositories.OAuthTokenException",
                "Icm.Api.Repositories.OAuthTokenRepository",
                "Icm.Api.Repositories.ServiceRequestRepository",
                "Icm.Api.Services.IOAuthTokenService",
                "Icm.Api.Services.IServiceRequestService",
                "Icm.Api.Services.OAuthTokenService",
                "Icm.Api.Services.ServiceRequestService",
            ];

            Assert.Equal(expected, exported);
        }

        [Fact]
        public void NothingFromTheTransportLayerEscapes()
        {
            // The same rule stated as an invariant, so a new wire contract is caught even
            // if someone updates the list above without thinking about it.
            string[] leaked = [.. Library.GetExportedTypes()
                .Select(type => type.FullName!)
                .Where(name => name.StartsWith("Icm.Api.Contracts.", StringComparison.Ordinal)
                    || name.Contains("Siebel", StringComparison.Ordinal)
                    || name.EndsWith("Api", StringComparison.Ordinal))];

            Assert.Empty(leaked);
        }

        [Fact]
        public void TheTestsThemselvesCanStillSeeTheInternals()
        {
            // If InternalsVisibleTo is ever dropped, this file stops compiling — but so
            // would half the suite, with a less obvious reason. Named here so the cause is
            // findable.
            Assert.NotNull(typeof(Icm.Api.Contracts.SiebelServiceRequest));
            Assert.NotNull(typeof(Icm.Api.IServiceRequestApi));
        }
    }
}
