namespace Icm.Api.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Ties the token service to the service-request repository, so callers deal in service
    /// requests and never in tokens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only thing it adds is the token, which is the whole point: the repository stays
    /// a pure data-access concern that can be handed any token, and authentication lives in
    /// exactly one place instead of at every call site.
    /// </para>
    /// <para>
    /// The credentials are fixed for the lifetime of the instance, because they identify
    /// <i>this application</i> to ICM. Anything that needs to act as a different identity
    /// wants its own instance, or the repository and a token of its own.
    /// </para>
    /// </remarks>
    public class ServiceRequestService : IServiceRequestService
    {
        private readonly IServiceRequestRepository _repository;
        private readonly IOAuthTokenService _tokenService;
        private readonly OAuthClientCredentials _credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRequestService"/> class.
        /// </summary>
        /// <param name="repository">ICM data access.</param>
        /// <param name="tokenService">The cached source of access tokens.</param>
        /// <param name="credentials">The credentials this application authenticates with.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public ServiceRequestService(
            IServiceRequestRepository repository,
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
        public async Task<ServiceRequestPage> SearchAsync(
            ServiceRequestQuery? query = null,
            CancellationToken cancellationToken = default) =>
            await _repository.SearchAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false),
                query,
                cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<ServiceRequest?> GetAsync(
            string serviceRequestKey,
            ServiceRequestReadOptions? options = null,
            CancellationToken cancellationToken = default) =>
            await _repository.GetAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false),
                serviceRequestKey,
                options,
                cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<ServiceRequest> CreateAsync(
            ServiceRequestInput input,
            CancellationToken cancellationToken = default) =>
            await _repository.CreateAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false),
                input,
                cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<ServiceRequest?> UpdateAsync(
            string serviceRequestKey,
            ServiceRequestInput input,
            CancellationToken cancellationToken = default) =>
            await _repository.UpdateAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false),
                serviceRequestKey,
                input,
                cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<ServiceRequest?> UpsertAsync(
            ServiceRequestInput input,
            CancellationToken cancellationToken = default) =>
            await _repository.UpsertAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false),
                input,
                cancellationToken).ConfigureAwait(false);

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(
            string serviceRequestKey,
            CancellationToken cancellationToken = default) =>
            await _repository.DeleteAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false),
                serviceRequestKey,
                cancellationToken).ConfigureAwait(false);

        private Task<string> TokenAsync(CancellationToken cancellationToken) =>
            _tokenService.GetTokenAsync(_credentials, cancellationToken);
    }
}
