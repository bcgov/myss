namespace Icm.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Ties the token service to the case repository, so callers deal in cases and never
    /// in tokens.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="ContactService"/>, for the same reasons: the only
    /// thing it adds is the token, and the credentials are fixed for the lifetime of the
    /// instance because they identify this application to ICM.
    /// </remarks>
    public class CaseService : ICaseService
    {
        private readonly ICaseRepository _repository;
        private readonly IOAuthTokenService _tokenService;
        private readonly OAuthClientCredentials _credentials;

        /// <summary>Initializes a new instance of the <see cref="CaseService"/> class.</summary>
        /// <param name="repository">ICM data access.</param>
        /// <param name="tokenService">The cached source of access tokens.</param>
        /// <param name="credentials">The credentials this application authenticates with.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public CaseService(
            ICaseRepository repository,
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
        public async Task<CasePage> SearchAsync(CaseQuery query, CancellationToken cancellationToken = default)
        {
            // Before the token, so a caller's mistake does not cost a round trip to the
            // authorization server.
            ArgumentNullException.ThrowIfNull(query);

            return await _repository.SearchAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), query, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Case?> GetAsync(
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caseKey);

            return await _repository.GetAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), caseKey, options, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CaseContact>> GetContactsAsync(
            string caseKey,
            CaseReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caseKey);

            return await _repository.GetContactsAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), caseKey, options, cancellationToken)
                .ConfigureAwait(false);
        }

        private Task<string> TokenAsync(CancellationToken cancellationToken) =>
            _tokenService.GetTokenAsync(_credentials, cancellationToken);
    }
}
