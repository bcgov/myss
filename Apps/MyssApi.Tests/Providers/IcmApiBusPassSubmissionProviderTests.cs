namespace Myss.Api.Tests.Providers
{
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Myss.Api.Configuration.Models;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// Tests for <see cref="IcmApiBusPassSubmissionProvider"/>, over a scripted
    /// handler so every byte that would reach the middleware is asserted on.
    /// </summary>
    public sealed class IcmApiBusPassSubmissionProviderTests : IDisposable
    {
        private const string BaseUrl = "http://icm-api.test/";
        private const string TokenEndpoint = "https://sso.test/realms/standard/protocol/openid-connect/token";

        private static readonly DateTimeOffset Start = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

        private readonly ScriptedHttpHandler _http = new();
        private readonly FakeTimeProvider _clock = new(Start);
        private readonly SingleClientFactory _factory;

        /// <summary>Initializes a new instance of the <see cref="IcmApiBusPassSubmissionProviderTests"/> class.</summary>
        public IcmApiBusPassSubmissionProviderTests()
        {
            _factory = new SingleClientFactory(_http);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _http.Dispose();
        }

        [Fact]
        public async Task Submit_PostsABearerAuthenticatedRequestToTheApplicationsRoute()
        {
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            BusPassSubmissionOutcomeModel outcome = await provider.SubmitAsync(Application(), CancellationToken.None);

            Assert.True(outcome.IsAccepted);
            Assert.Equal("1-TEST-0001", outcome.ApplicationNumber);

            Assert.Equal(2, _http.Requests.Count);
            RecordedRequest token = _http.Requests[0];
            Assert.Equal(TokenEndpoint, token.Uri);
            Assert.Contains("grant_type=client_credentials", token.Body, StringComparison.Ordinal);
            Assert.Contains("client_id=myss-api", token.Body, StringComparison.Ordinal);
            Assert.Contains("client_secret=s3cret", token.Body, StringComparison.Ordinal);

            RecordedRequest post = _http.Requests[1];
            Assert.Equal(HttpMethod.Post, post.Method);
            Assert.Equal(BaseUrl + IcmApiBusPassSubmissionProvider.ApplicationsPath, post.Uri);
            Assert.Equal("Bearer tok-1", post.Authorization);
            Assert.Contains("\"requestType\":\"NewApplication\"", post.Body, StringComparison.Ordinal);
            Assert.Contains("\"firstName\":\"Ada\"", post.Body, StringComparison.Ordinal);
            Assert.Contains("\"dateOfBirth\":\"1950-12-10\"", post.Body, StringComparison.Ordinal);
            Assert.Equal([IcmApiBusPassSubmissionProvider.HttpClientName], _factory.Names);
        }

        [Fact]
        public async Task Submit_SendsTheScopeOnlyWhenConfigured()
        {
            using IcmApiBusPassSubmissionProvider provider = NewProvider(scope: "icm-api");

            await provider.SubmitAsync(Application(), CancellationToken.None);

            Assert.Contains("scope=icm-api", _http.Requests[0].Body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Submit_ReusesTheTokenUntilShortlyBeforeItExpires()
        {
            // expires_in is 300s and the safety margin 30s: still valid at
            // 269s, refreshed at 271s.
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            await provider.SubmitAsync(Application(), CancellationToken.None);
            _clock.Advance(TimeSpan.FromSeconds(269));
            await provider.SubmitAsync(Application(), CancellationToken.None);
            Assert.Equal(1, _http.Requests.Count(r => r.Uri == TokenEndpoint));

            _clock.Advance(TimeSpan.FromSeconds(2));
            await provider.SubmitAsync(Application(), CancellationToken.None);
            Assert.Equal(2, _http.Requests.Count(r => r.Uri == TokenEndpoint));
        }

        [Fact]
        public async Task BusinessRejection_IsAnOutcomeNotAnException()
        {
            _http.ApplicationBody = """{"applicationNumber":"1-ERR-0002","errorCode":"NO_MATCH","errorMessage":"Contact or Case Match not Found","status":"Error"}""";
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            BusPassSubmissionOutcomeModel outcome = await provider.SubmitAsync(Application(), CancellationToken.None);

            Assert.False(outcome.IsAccepted);
            Assert.Equal("NO_MATCH", outcome.ErrorCode);
            Assert.Equal("1-ERR-0002", outcome.ApplicationNumber);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task FailureStatusFromTheMiddleware_ThrowsUnavailableWithTheStatus(HttpStatusCode status)
        {
            _http.ApplicationStatus = status;
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal((int)status, ex.StatusCode);
        }

        [Fact]
        public async Task TokenEndpointRejection_ThrowsUnavailableAndNeverPostsTheApplication()
        {
            _http.TokenStatus = HttpStatusCode.Unauthorized;
            _http.TokenBody = """{"error":"invalid_client"}""";
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(401, ex.StatusCode);
            Assert.False(ex.MayHaveReachedIcm);
            Assert.Single(_http.Requests);
        }

        [Theory]
        [InlineData(HttpStatusCode.ServiceUnavailable, "ICM.BUSPASS.NOT_CONFIGURED", false)]
        [InlineData(HttpStatusCode.ServiceUnavailable, "ICM.BUSPASS.TOKEN_UNAVAILABLE", false)]
        [InlineData(HttpStatusCode.BadGateway, "ICM.BUSPASS.UNREACHABLE", false)]
        [InlineData(HttpStatusCode.GatewayTimeout, "ICM.BUSPASS.TIMEOUT", true)]
        [InlineData(HttpStatusCode.BadGateway, "ICM.BUSPASS.UPSTREAM_ERROR", true)]
        [InlineData(HttpStatusCode.BadGateway, "ICM.BUSPASS.UNUSABLE_RESPONSE", true)]
        public async Task TheMiddlewaresKeyword_IsCarriedAndSaysWhetherIcmMayHaveTheRequest(
            HttpStatusCode status, string keyword, bool mayHaveReachedIcm)
        {
            _http.ApplicationStatus = status;
            _http.ApplicationBody = $$"""{"title":"The request could not be submitted to ICM.","status":{{(int)status}},"detail":"x","keyword":"{{keyword}}"}""";
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(keyword, ex.Keyword);
            Assert.Equal(mayHaveReachedIcm, ex.MayHaveReachedIcm);
        }

        [Fact]
        public async Task AFailureWithNoKeyword_IsTreatedAsPossiblyDelivered()
        {
            // Unclassified means unknown, and unknown must not invite a resend.
            _http.ApplicationStatus = HttpStatusCode.InternalServerError;
            _http.ApplicationBody = "<html>proxy error</html>";
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Null(ex.Keyword);
            Assert.True(ex.MayHaveReachedIcm);
        }

        [Fact]
        public async Task AResponseWhoseBodyFailsToArrive_IsTreatedAsPossiblyDelivered()
        {
            // HttpClient buffers the body inside SendAsync, so a connection that
            // drops mid-body surfaces as an HttpRequestException after the request
            // was on the wire. That is not a connect failure and must not invite a
            // resend.
            _http.ApplicationStatus = HttpStatusCode.BadGateway;
            _http.ApplicationBodyFails = true;
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Null(ex.Keyword);
            Assert.True(ex.MayHaveReachedIcm);
        }

        [Theory]
        [InlineData(HttpRequestError.NameResolutionError, false)]
        [InlineData(HttpRequestError.ConnectionError, false)]
        [InlineData(HttpRequestError.SecureConnectionError, false)]
        [InlineData(HttpRequestError.ResponseEnded, true)]
        [InlineData(HttpRequestError.Unknown, true)]
        public async Task OnlyAFailureToConnect_ProvesNothingWasDelivered(HttpRequestError error, bool mayHaveReachedIcm)
        {
            _http.ApplicationThrow = new HttpRequestException(error, "transport failure");
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(mayHaveReachedIcm, ex.MayHaveReachedIcm);
            Assert.Equal(2, _http.Requests.Count);
        }

        [Fact]
        public async Task ConnectionFailure_ThrowsUnavailableWithTheCause()
        {
            _http.Throw = new HttpRequestException("connection refused", null, null);
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.IsType<HttpRequestException>(ex.InnerException);
            Assert.Null(ex.StatusCode);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("not json")]
        public async Task UnusableSuccessBody_ThrowsUnavailable(string body)
        {
            _http.ApplicationBody = body;
            using IcmApiBusPassSubmissionProvider provider = NewProvider();

            IcmApiUnavailableException ex = await Assert.ThrowsAsync<IcmApiUnavailableException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Equal(200, ex.StatusCode);
        }

        [Fact]
        public async Task MissingCredentials_FailLoudlyBeforeAnyCall()
        {
            // Same shape as CDOGS: the base URL is checked at startup, the
            // secret at first use, so a checkout without secrets still boots.
            using IcmApiBusPassSubmissionProvider provider = NewProvider(clientSecret: null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.SubmitAsync(Application(), CancellationToken.None));

            Assert.Empty(_http.Requests);
        }

        private static BusPassApplicationModel Application() => new()
        {
            RequestType = BusPassRequestType.NewApplication,
            ApplicantType = BusPassApplicantType.Over65,
            SocialInsuranceNumber = "046454286",
            FirstName = "Ada",
            LastName = "Lovelace",
            DateOfBirth = new DateOnly(1950, 12, 10),
            PhoneNumber = "2505550199",
            PhoneType = BusPassPhoneType.Home,
            PreferredContactMethod = BusPassContactMethod.Phone,
            ResidentialAddress = new BusPassAddressModel { Line1 = "501 Belleville St", City = "Victoria", Province = "BC", PostalCode = "V8V 1X4" },
        };

        private IcmApiBusPassSubmissionProvider NewProvider(string? clientSecret = "s3cret", string? scope = null)
        {
            var serviceAccount = new OidcServiceAccountConfig
            {
                TokenEndpoint = TokenEndpoint,
                ClientId = "myss-api",
                ClientSecret = clientSecret,
                Scope = scope,
            };

            return new IcmApiBusPassSubmissionProvider(
                NullLogger<IcmApiBusPassSubmissionProvider>.Instance,
                _factory,
                Options.Create(serviceAccount),
                _clock);
        }

        private sealed record RecordedRequest(HttpMethod Method, string Uri, string? Authorization, string Body);

        /// <summary>
        /// Answers the token endpoint and the applications route with scripted
        /// bodies, recording each request's content before it is disposed.
        /// </summary>
        private sealed class ScriptedHttpHandler : HttpMessageHandler
        {
            public List<RecordedRequest> Requests { get; } = [];

            public HttpStatusCode TokenStatus { get; set; } = HttpStatusCode.OK;

            public string TokenBody { get; set; } = """{"access_token":"tok-1","expires_in":300,"token_type":"Bearer"}""";

            public HttpStatusCode ApplicationStatus { get; set; } = HttpStatusCode.OK;

            public string ApplicationBody { get; set; } = """{"applicationNumber":"1-TEST-0001","status":"Ready"}""";

            public Exception? Throw { get; set; }

            /// <summary>Thrown for the applications route only, after the token was issued.</summary>
            public Exception? ApplicationThrow { get; set; }

            /// <summary>Makes the applications route's body fail to read (the connection dropped mid-body).</summary>
            public bool ApplicationBodyFails { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add(new RecordedRequest(
                    request.Method,
                    request.RequestUri!.AbsoluteUri,
                    request.Headers.Authorization?.ToString(),
                    body));

                if (Throw is not null)
                {
                    throw Throw;
                }

                bool isToken = request.RequestUri.AbsoluteUri == TokenEndpoint;
                if (!isToken && ApplicationThrow is not null)
                {
                    throw ApplicationThrow;
                }

                return new HttpResponseMessage(isToken ? TokenStatus : ApplicationStatus)
                {
                    Content = !isToken && ApplicationBodyFails
                        ? new UnreadableContent()
                        : new StringContent(isToken ? TokenBody : ApplicationBody, Encoding.UTF8, "application/json"),
                };
            }
        }

        /// <summary>A body whose read fails the way a dropped connection does.</summary>
        private sealed class UnreadableContent : HttpContent
        {
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
                throw new IOException("The response ended prematurely.");

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }

        private sealed class SingleClientFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;

            public SingleClientFactory(HttpMessageHandler handler)
            {
                _handler = handler;
            }

            public List<string> Names { get; } = [];

            public HttpClient CreateClient(string name)
            {
                Names.Add(name);
                return new HttpClient(_handler, disposeHandler: false) { BaseAddress = new Uri(BaseUrl) };
            }
        }
    }
}
