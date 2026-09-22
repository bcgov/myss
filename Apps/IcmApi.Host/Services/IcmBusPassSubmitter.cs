namespace Icm.Api.Host.Services
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Host.Configuration.Models;
    using Icm.Api.Host.Contracts;
    using Icm.Api.Models;
    using Icm.Api.Repositories;
    using Icm.Api.Services;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Refit;

    /// <summary>
    /// <see cref="IBusPassSubmitter"/> over the ICM client library: get a token,
    /// then submit. The two calls are made separately, as IcmApi.Console does, so
    /// a rejected credential and a rejected ICM request are told apart rather than
    /// arriving as the same exception type from the same line.
    /// </summary>
    /// <remarks>
    /// Nothing here retries. ICM files a service request on every call and has no
    /// idempotency key of its own, so a repeat after an ambiguous failure is
    /// MyssApi's decision, made with the submission key it sends.
    /// </remarks>
    public sealed class IcmBusPassSubmitter : IBusPassSubmitter
    {
        private readonly IBusPassRepository _repository;
        private readonly IOAuthTokenService _tokenService;
        private readonly IcmAuthConfig _auth;
        private readonly ILogger<IcmBusPassSubmitter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IcmBusPassSubmitter"/> class.
        /// </summary>
        /// <param name="repository">ICM workflow access.</param>
        /// <param name="tokenService">The cached source of access tokens.</param>
        /// <param name="config">The ICM settings.</param>
        /// <param name="logger">Injected logger.</param>
        public IcmBusPassSubmitter(
            IBusPassRepository repository,
            IOAuthTokenService tokenService,
            IOptions<IcmConfig> config,
            ILogger<IcmBusPassSubmitter> logger)
        {
            ArgumentNullException.ThrowIfNull(config);

            _repository = repository;
            _tokenService = tokenService;
            _auth = config.Value.Auth;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<BusPassResult> SubmitAsync(BusPassApplication application, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(application);

            Uri? tokenUrl = _auth.ResolveTokenUrl();
            if (!_auth.HasCredentials || tokenUrl is null)
            {
                throw new IcmUpstreamException(
                    BusPassKeywords.NotConfigured,
                    StatusCodes.Status503ServiceUnavailable,
                    "The middleware has no ICM credentials configured.");
            }

            var credentials = new OAuthClientCredentials
            {
                TokenUrl = tokenUrl,
                ClientId = _auth.ClientId,
                ClientSecret = _auth.ClientSecret,
                Scopes = _auth.Scopes.Count == 0 ? null : _auth.Scopes,
            };

            string token = await GetTokenAsync(credentials, cancellationToken).ConfigureAwait(false);

            try
            {
                return await _repository.SubmitAsync(token, application, cancellationToken).ConfigureAwait(false);
            }
            catch (ApiRequestException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Refit wraps what HttpClient threw before any response arrived; a
                // cancellation nobody asked for is the client's own timeout.
                throw ex.InnerException is OperationCanceledException or TimeoutException
                    ? Fail(BusPassKeywords.Timeout, StatusCodes.Status504GatewayTimeout, "ICM did not answer in time.", ex)
                    : Fail(BusPassKeywords.Unreachable, StatusCodes.Status502BadGateway, "ICM could not be reached.", ex);
            }
            catch (ApiException ex)
            {
                throw Fail(
                    BusPassKeywords.UpstreamError,
                    StatusCodes.Status502BadGateway,
                    $"ICM answered {(int)ex.StatusCode} to the bus pass submission.",
                    ex);
            }
            catch (HttpRequestException ex)
            {
                throw Fail(BusPassKeywords.Unreachable, StatusCodes.Status502BadGateway, "ICM could not be reached.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw Fail(BusPassKeywords.Timeout, StatusCodes.Status504GatewayTimeout, "ICM did not answer in time.", ex);
            }
            catch (IcmResponseException ex)
            {
                throw Fail(
                    BusPassKeywords.UnusableResponse,
                    StatusCodes.Status502BadGateway,
                    "ICM reported success but its answer was not usable.",
                    ex);
            }
        }

        private async Task<string> GetTokenAsync(OAuthClientCredentials credentials, CancellationToken cancellationToken)
        {
            try
            {
                return await _tokenService.GetTokenAsync(credentials, cancellationToken).ConfigureAwait(false);
            }
            catch (ApiRequestException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw Fail(BusPassKeywords.TokenUnavailable, StatusCodes.Status503ServiceUnavailable, "The authorization server for ICM could not be reached.", ex);
            }
            catch (ApiException ex)
            {
                // invalid_client lands here as a 401: the clearest sign a deployment
                // has the wrong secret, so it is logged as an error.
                throw Fail(
                    BusPassKeywords.TokenUnavailable,
                    StatusCodes.Status503ServiceUnavailable,
                    $"The authorization server for ICM answered {(int)ex.StatusCode}.",
                    ex);
            }
            catch (HttpRequestException ex)
            {
                throw Fail(BusPassKeywords.TokenUnavailable, StatusCodes.Status503ServiceUnavailable, "The authorization server for ICM could not be reached.", ex);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw Fail(BusPassKeywords.TokenUnavailable, StatusCodes.Status503ServiceUnavailable, "The authorization server for ICM did not answer in time.", ex);
            }
            catch (OAuthTokenException ex)
            {
                throw Fail(BusPassKeywords.TokenUnavailable, StatusCodes.Status503ServiceUnavailable, "The authorization server for ICM returned an unusable answer.", ex);
            }
        }

        private IcmUpstreamException Fail(string keyword, int statusCode, string message, Exception inner)
        {
            // The exception's own message, not any response body: ICM's bodies can
            // carry record data, and the log must stay free of it.
            _logger.LogError(inner, "Bus pass submission failed: {Keyword} ({StatusCode}). {Message}", keyword, statusCode, message);
            return new IcmUpstreamException(keyword, statusCode, message, inner);
        }
    }
}
