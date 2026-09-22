namespace Icm.Api.Host.Tests
{
    using System.Net;
    using Icm.Api.Host.Configuration.Models;
    using Icm.Api.Host.Contracts;
    using Icm.Api.Host.Services;
    using Icm.Api.Host.Tests.TestDoubles;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Refit;

    /// <summary>
    /// How the submitter tells the failures apart: a rejected credential, an
    /// unreachable ICM, a slow ICM and a failing ICM each get their own keyword
    /// and status, and a business rejection is not a failure at all.
    /// </summary>
    public class IcmBusPassSubmitterTests
    {
        private readonly FakeBusPassRepository _repository = new();
        private readonly FakeOAuthTokenService _tokens = new();

        [Fact]
        public async Task Success_SubmitsWithTheTokenAndReturnsTheResult()
        {
            IcmBusPassSubmitter submitter = Create();
            _tokens.Token = "tok-xyz";
            _repository.Result = new BusPassResult { ApplicationNumber = "1-OK", Status = "SUCCESS" };

            BusPassResult result = await submitter.SubmitAsync(Application(), CancellationToken.None);

            Assert.Equal("1-OK", result.ApplicationNumber);
            (string token, BusPassApplication sent) = Assert.Single(_repository.Calls);
            Assert.Equal("tok-xyz", token);
            Assert.Equal("key-1", sent.SubmissionKey);

            OAuthClientCredentials credentials = Assert.Single(_tokens.Requests);
            Assert.Equal("https://sso.test.invalid/auth/realms/icm/protocol/openid-connect/token", credentials.TokenUrl?.AbsoluteUri);
            Assert.Equal("icm-client", credentials.ClientId);
            Assert.Equal("openid data", credentials.GetScopeParameter());
        }

        [Fact]
        public async Task BusinessRejection_IsAResultNotAFailure()
        {
            IcmBusPassSubmitter submitter = Create();
            _repository.Result = new BusPassResult { ApplicationNumber = "1-ERR", ErrorCode = "NO_MATCH", ErrorMessage = "Contact or Case Match not Found" };

            BusPassResult result = await submitter.SubmitAsync(Application(), CancellationToken.None);

            Assert.Equal("NO_MATCH", result.ErrorCode);
        }

        [Fact]
        public async Task NoCredentials_Is503NotConfigured_AndNothingIsCalled()
        {
            IcmBusPassSubmitter submitter = Create(withCredentials: false);

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.NotConfigured, ex.Keyword);
            Assert.Equal(503, ex.StatusCode);
            Assert.Empty(_tokens.Requests);
            Assert.Empty(_repository.Calls);
        }

        [Fact]
        public async Task TokenEndpointRejectsTheClient_Is503TokenUnavailable()
        {
            IcmBusPassSubmitter submitter = Create();
            _tokens.Failure = await ApiFailure(HttpStatusCode.Unauthorized, "https://sso.test.invalid/token");

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.TokenUnavailable, ex.Keyword);
            Assert.Equal(503, ex.StatusCode);
            Assert.Contains("401", ex.Message, StringComparison.Ordinal);
            Assert.Empty(_repository.Calls);
        }

        [Fact]
        public async Task TokenEndpointUnreachable_Is503TokenUnavailable()
        {
            IcmBusPassSubmitter submitter = Create();
            _tokens.Failure = RequestFailure(new HttpRequestException("No such host"), "https://sso.test.invalid/token");

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.TokenUnavailable, ex.Keyword);
        }

        [Fact]
        public async Task TokenResponseUnusable_Is503TokenUnavailable()
        {
            IcmBusPassSubmitter submitter = Create();
            _tokens.Failure = new OAuthTokenException("no access_token");

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.TokenUnavailable, ex.Keyword);
        }

        [Fact]
        public async Task IcmAnswersAFailureStatus_Is502UpstreamError()
        {
            IcmBusPassSubmitter submitter = Create();
            _repository.Failure = await ApiFailure(HttpStatusCode.Forbidden, "https://icm.test.invalid/gov/v1.0/workflow/x");

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.UpstreamError, ex.Keyword);
            Assert.Equal(502, ex.StatusCode);
            Assert.Contains("403", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task IcmUnreachable_Is502Unreachable()
        {
            IcmBusPassSubmitter submitter = Create();
            _repository.Failure = RequestFailure(new HttpRequestException("Connection refused"), "https://icm.test.invalid/gov/v1.0/workflow/x");

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.Unreachable, ex.Keyword);
            Assert.Equal(502, ex.StatusCode);
        }

        [Fact]
        public async Task IcmTimesOut_Is504Timeout()
        {
            IcmBusPassSubmitter submitter = Create();
            _repository.Failure = RequestFailure(
                new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout", new TimeoutException()),
                "https://icm.test.invalid/gov/v1.0/workflow/x");

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.Timeout, ex.Keyword);
            Assert.Equal(504, ex.StatusCode);
        }

        [Fact]
        public async Task IcmSuccessWithNoResult_Is502UnusableResponse()
        {
            IcmBusPassSubmitter submitter = Create();
            _repository.Failure = new IcmResponseException("ICM reported the bus pass submission succeeded but returned no result.");

            IcmUpstreamException ex = await Assert.ThrowsAsync<IcmUpstreamException>(() => submitter.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(BusPassKeywords.UnusableResponse, ex.Keyword);
            Assert.Equal(502, ex.StatusCode);
        }

        [Fact]
        public async Task CallerCancellation_IsNotReportedAsATimeout()
        {
            IcmBusPassSubmitter submitter = Create();
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            _repository.Failure = new OperationCanceledException(cancellation.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => submitter.SubmitAsync(Application(), cancellation.Token));
        }

        private IcmBusPassSubmitter Create(bool withCredentials = true)
        {
            var config = new IcmConfig
            {
                BaseUrl = new Uri("https://icm.test.invalid/gov/v1.0"),
                Auth = new IcmAuthConfig
                {
                    BaseUrl = new Uri("https://sso.test.invalid/auth"),
                    Realm = "icm",
                    ClientId = withCredentials ? "icm-client" : null,
                    ClientSecret = withCredentials ? "not-a-real-secret" : null,
                },
            };
            config.Auth.Scopes.Add("openid");
            config.Auth.Scopes.Add("data");

            return new IcmBusPassSubmitter(_repository, _tokens, Options.Create(config), NullLogger<IcmBusPassSubmitter>.Instance);
        }

        private static BusPassApplication Application() => new()
        {
            SubmissionKey = "key-1",
            RequestType = BusPassRequestType.NewApplication,
            FirstName = "Myss",
            LastName = "IntegrationTest",
        };

        private static async Task<ApiException> ApiFailure(HttpStatusCode status, string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            using var response = new HttpResponseMessage(status) { Content = new StringContent("{}") };
            return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
        }

        private static ApiRequestException RequestFailure(Exception inner, string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            return new ApiRequestException(request, HttpMethod.Post, new RefitSettings(), inner);
        }
    }
}
