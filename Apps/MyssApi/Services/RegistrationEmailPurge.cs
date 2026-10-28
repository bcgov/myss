namespace Myss.Api.Services
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Configuration.Models;
    using Myss.Data;

    /// <summary>
    /// Deletes, once an hour, the registration email proofs no registration can
    /// use any more (MYSS-295): a citizen who leaves registration at Step 2
    /// leaves an address behind, and this is what keeps it from staying there.
    /// The rule is <see cref="EmailVerificationService.PurgeStaleRegistrationEmailsAsync"/>.
    /// Safe on every instance at once: each deletes what is stale, and a row
    /// gone already is simply not found.
    /// </summary>
    public sealed class RegistrationEmailPurge : BackgroundService
    {
        /// <summary>How often stale proofs are deleted.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopes;
        private readonly TimeProvider _timeProvider;
        private readonly EmailVerificationConfig _config;
        private readonly ILogger<RegistrationEmailPurge> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistrationEmailPurge"/> class.
        /// </summary>
        /// <param name="scopes">Creates a scope, and a forms context, per run.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <param name="config">The link's lifetime, which is the registration session.</param>
        /// <param name="logger">Injected logger.</param>
        public RegistrationEmailPurge(
            IServiceScopeFactory scopes,
            TimeProvider timeProvider,
            EmailVerificationConfig config,
            ILogger<RegistrationEmailPurge> logger)
        {
            _scopes = scopes;
            _timeProvider = timeProvider;
            _config = config;
            _logger = logger;
        }

        /// <inheritdoc/>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval, _timeProvider);
            do
            {
                try
                {
                    using IServiceScope scope = _scopes.CreateScope();
                    FormsDbContext db = scope.ServiceProvider.GetRequiredService<FormsDbContext>();
                    int deleted = await EmailVerificationService.PurgeStaleRegistrationEmailsAsync(
                        db,
                        _timeProvider.GetUtcNow(),
                        _config.TokenLifetime,
                        stoppingToken);
                    if (deleted > 0)
                    {
                        _logger.LogInformation("Deleted {Count} stale registration email proofs", deleted);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (DbException ex)
                {
                    // A failed run (the database briefly away, say) is tried
                    // again at the next interval; it must not stop the API.
                    _logger.LogWarning(ex, "Registration email purge failed; it will run again");
                }
                catch (InvalidOperationException ex)
                {
                    // What EF raises once its connection retries give up.
                    _logger.LogWarning(ex, "Registration email purge failed; it will run again");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
