namespace Icm.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>
    /// Ties the token service to the prospect repository, so callers deal in prospects and
    /// never in tokens.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="CaseService"/>, for the same reasons: the only thing
    /// it adds is the token, and the credentials are fixed for the lifetime of the
    /// instance because they identify this application to ICM.
    /// </remarks>
    public class ProspectService : IProspectService
    {
        private readonly IProspectRepository _repository;
        private readonly IOAuthTokenService _tokenService;
        private readonly OAuthClientCredentials _credentials;

        /// <summary>Initializes a new instance of the <see cref="ProspectService"/> class.</summary>
        /// <param name="repository">ICM data access.</param>
        /// <param name="tokenService">The cached source of access tokens.</param>
        /// <param name="credentials">The credentials this application authenticates with.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public ProspectService(
            IProspectRepository repository,
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
        public async Task<ProspectPage> SearchAsync(ProspectQuery query, CancellationToken cancellationToken = default)
        {
            // The mapper's whole judgement before the token, so a caller's mistake costs
            // no round trip to the authorization server; the repository vets again.
            ProspectMapper.Validate(query);

            return await _repository.SearchAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), query, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Prospect?> GetAsync(
            string prospectKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prospectKey);

            return await _repository.GetAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), prospectKey, options, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Prospect>> GetForServiceRequestAsync(
            string serviceRequestKey,
            ProspectReadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestKey);

            return await _repository.GetForServiceRequestAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), serviceRequestKey, options, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Prospect> CreateAsync(ProspectInput input, CancellationToken cancellationToken = default)
        {
            ProspectMapper.Validate(input);

            return await _repository.CreateAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), input, cancellationToken)
                .ConfigureAwait(false);
        }

        private Task<string> TokenAsync(CancellationToken cancellationToken) =>
            _tokenService.GetTokenAsync(_credentials, cancellationToken);
    }
}
