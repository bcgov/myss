namespace Icm.Api.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Ties the token service to the bus pass repository, so callers deal in bus pass
    /// requests and never in tokens — the same one job
    /// <see cref="ServiceRequestService"/> does for service requests.
    /// </summary>
    public class BusPassService : IBusPassService
    {
        private readonly IBusPassRepository _repository;
        private readonly IOAuthTokenService _tokenService;
        private readonly OAuthClientCredentials _credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="BusPassService"/> class.
        /// </summary>
        /// <param name="repository">ICM workflow access.</param>
        /// <param name="tokenService">The cached source of access tokens.</param>
        /// <param name="credentials">The credentials this application authenticates with.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BusPassService(
            IBusPassRepository repository,
            IOAuthTokenService tokenService,
            OAuthClientCredentials credentials)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(tokenService);
            ArgumentNullException.ThrowIfNull(credentials);

            _repository = repository;
            _tokenService = tokenService;
            _credentials = credentials;
        }

        /// <inheritdoc/>
        public async Task<BusPassResult> SubmitAsync(
            BusPassApplication application,
            CancellationToken cancellationToken = default) =>
            await _repository.SubmitAsync(
                await _tokenService.GetTokenAsync(_credentials, cancellationToken).ConfigureAwait(false),
                application,
                cancellationToken).ConfigureAwait(false);
    }
}
