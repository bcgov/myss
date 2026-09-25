namespace Icm.Api.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Ties the token service to the contact repository, so callers deal in contacts and
    /// never in tokens.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="ServiceRequestService"/>, for the same reasons: the
    /// only thing it adds is the token, and the credentials are fixed for the lifetime of
    /// the instance because they identify this application to ICM.
    /// </remarks>
    public class ContactService : IContactService
    {
        private readonly IContactRepository _repository;
        private readonly IOAuthTokenService _tokenService;
        private readonly OAuthClientCredentials _credentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContactService"/> class.
        /// </summary>
        /// <param name="repository">ICM data access.</param>
        /// <param name="tokenService">The cached source of access tokens.</param>
        /// <param name="credentials">The credentials this application authenticates with.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public ContactService(
            IContactRepository repository,
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
        public async Task<ContactPage> SearchAsync(
            ContactQuery query,
            CancellationToken cancellationToken = default)
        {
            // Before the token, so a caller's mistake does not cost a round trip to the
            // authorization server.
            ArgumentNullException.ThrowIfNull(query);

            return await _repository.SearchAsync(
                await _tokenService.GetTokenAsync(_credentials, cancellationToken).ConfigureAwait(false),
                query,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
