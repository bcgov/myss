namespace Icm.Api.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Icm.Api.Contracts;
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>Ties the token service to the activity repository, so callers deal in activities and never in tokens.</summary>
    /// <remarks>The same shape as <see cref="ProspectService"/>, for the same reasons.</remarks>
    public class ActivityService : IActivityService
    {
        private readonly IActivityRepository _repository;
        private readonly IOAuthTokenService _tokenService;
        private readonly OAuthClientCredentials _credentials;

        /// <summary>Initializes a new instance of the <see cref="ActivityService"/> class.</summary>
        /// <param name="repository">ICM data access.</param>
        /// <param name="tokenService">The cached source of access tokens.</param>
        /// <param name="credentials">The credentials this application authenticates with.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public ActivityService(IActivityRepository repository, IOAuthTokenService tokenService, OAuthClientCredentials credentials)
        {
            ArgumentNullException.ThrowIfNull(repository);
            ArgumentNullException.ThrowIfNull(tokenService);
            ArgumentNullException.ThrowIfNull(credentials);

            _repository = repository;
            _tokenService = tokenService;
            _credentials = credentials;
        }

        /// <inheritdoc/>
        public async Task<ActivityPage> SearchAsync(ActivityQuery query, CancellationToken cancellationToken = default)
        {
            ActivityMapper.Validate(query);
            return await _repository.SearchAsync(await TokenAsync(cancellationToken).ConfigureAwait(false), query, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Activity?> GetAsync(string activityKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(activityKey);
            return await _repository.GetAsync(await TokenAsync(cancellationToken).ConfigureAwait(false), activityKey, options, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Activity>> GetForServiceRequestAsync(
            string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestKey);
            return await _repository.GetForServiceRequestAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), serviceRequestKey, options, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<Activity> CreateAsync(ActivityInput input, CancellationToken cancellationToken = default)
        {
            ActivityMapper.Validate(input);
            return await _repository.CreateAsync(await TokenAsync(cancellationToken).ConfigureAwait(false), input, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ActivityPlanPage> SearchPlansAsync(ActivityPlanQuery query, CancellationToken cancellationToken = default)
        {
            ActivityMapper.Validate(query);
            return await _repository.SearchPlansAsync(await TokenAsync(cancellationToken).ConfigureAwait(false), query, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<ActivityPlan>> GetPlansForServiceRequestAsync(
            string serviceRequestKey, ActivityReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceRequestKey);
            return await _repository.GetPlansForServiceRequestAsync(
                await TokenAsync(cancellationToken).ConfigureAwait(false), serviceRequestKey, options, cancellationToken).ConfigureAwait(false);
        }

        private Task<string> TokenAsync(CancellationToken cancellationToken) => _tokenService.GetTokenAsync(_credentials, cancellationToken);
    }
}
