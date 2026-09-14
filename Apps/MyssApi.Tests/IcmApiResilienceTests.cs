namespace Myss.Api.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http.Resilience;
    using Myss.Api.Configuration;
    using Myss.Api.Configuration.Models;
    using Polly;
    using Polly.Timeout;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="IcmApiResilience"/>: the options must satisfy the
    /// standard handler's own validation, and only a request that never left
    /// the process may be retried.
    /// </summary>
    public class IcmApiResilienceTests
    {
        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(120)]
        public void ConfiguredPipeline_PassesTheHandlersOwnValidation(int timeoutSeconds)
        {
            // The standard handler validates its options when the client is
            // first built (attempt <= total, sampling >= 2 x attempt), which the
            // endpoint tests never reach because they swap the provider out.
            var config = new IcmApiConfig { BaseUrl = new Uri("http://icm-api.test/"), TimeoutSeconds = timeoutSeconds };
            var services = new ServiceCollection();
            services.AddHttpClient("icm").AddStandardResilienceHandler(options => IcmApiResilience.Configure(options, config));
            using ServiceProvider provider = services.BuildServiceProvider();

            using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("icm");

            Assert.NotNull(client);
        }

        [Fact]
        public void Configure_DerivesEveryTimeoutFromTheAttemptTimeout()
        {
            var config = new IcmApiConfig { TimeoutSeconds = 30 };
            var options = new HttpStandardResilienceOptions();

            IcmApiResilience.Configure(options, config);

            Assert.Equal(TimeSpan.FromSeconds(30), options.AttemptTimeout.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(95), options.TotalRequestTimeout.Timeout);
            Assert.Equal(TimeSpan.FromSeconds(60), options.CircuitBreaker.SamplingDuration);
            Assert.Equal(IcmApiResilience.MaxRetryAttempts, options.Retry.MaxRetryAttempts);
        }

        [Theory]
        [InlineData(HttpRequestError.NameResolutionError, true)]
        [InlineData(HttpRequestError.ConnectionError, true)]
        [InlineData(HttpRequestError.SecureConnectionError, true)]
        [InlineData(HttpRequestError.ResponseEnded, false)]
        [InlineData(HttpRequestError.Unknown, false)]
        public void OnlyFailuresBeforeAnyByteWasSent_AreRetried(HttpRequestError error, bool retried)
        {
            var outcome = Outcome.FromException<HttpResponseMessage>(
                new HttpRequestException(error, "failed"));

            Assert.Equal(retried, IcmApiResilience.IsConnectFailure(outcome));
        }

        [Fact]
        public void TimeoutsAndFailureStatuses_AreNeverRetried()
        {
            // ICM files a service request on every call it receives; a request
            // that timed out may well have arrived.
            using var badGateway = new HttpResponseMessage(HttpStatusCode.BadGateway);

            Assert.False(IcmApiResilience.IsConnectFailure(Outcome.FromException<HttpResponseMessage>(new TimeoutRejectedException())));
            Assert.False(IcmApiResilience.IsConnectFailure(Outcome.FromResult(badGateway)));
        }
    }
}
