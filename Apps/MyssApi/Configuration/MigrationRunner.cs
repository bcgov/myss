namespace Myss.Api.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Api.Data;
    using Myss.Api.Intake;
    using Myss.Api.Platform;

    /// <summary>
    /// The API's migrate mode: <c>MyssApi --migrate</c> applies every pending
    /// EF migration of every context, in schema order, and exits. It is what
    /// the deployment pipeline runs as a Kubernetes Job from the freshly built
    /// image, before the rollout restart (handbook Part 7.3: migrations as
    /// Jobs, never by hand). The same image carries the code and its schema,
    /// so the two cannot drift.
    /// </summary>
    /// <remarks>
    /// It deliberately does not go through <see cref="Startup"/>: the API fails
    /// closed without object storage and ICM settings, and a migration needs
    /// only the connection strings. EF Core takes a database lock while
    /// migrating, so two Jobs cannot interleave.
    /// </remarks>
    public static class MigrationRunner
    {
        /// <summary>The command-line switch that selects this mode (local use).</summary>
        public const string Switch = "--migrate";

        /// <summary>
        /// The environment variable that selects this mode. The deployment Job
        /// uses it: the S2I image starts the app through a run script with no
        /// entrypoint, so container args cannot reach the app, but env can.
        /// </summary>
        public const string EnvironmentVariable = "Myss_MigrateAndExit";

        /// <summary>The connection string every context falls back to.</summary>
        private const string DefaultConnectionName = "FormsDb";

        /// <summary>
        /// Whether the arguments or the environment ask for migrate mode.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>True when <see cref="Switch"/> is present or <see cref="EnvironmentVariable"/> is "true".</returns>
        public static bool IsRequested(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            return args.Contains(Switch, StringComparer.Ordinal)
                || IsTrue(Environment.GetEnvironmentVariable(EnvironmentVariable));
        }

        private static bool IsTrue(string? value)
        {
            return bool.TryParse(value, out bool parsed) && parsed;
        }

        /// <summary>
        /// The arguments with the switch removed, for the host builder.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The remaining arguments.</returns>
        public static string[] WithoutSwitch(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            return args.Where(a => !string.Equals(a, Switch, StringComparison.Ordinal)).ToArray();
        }

        /// <summary>
        /// Applies pending migrations for every context, in order: forms,
        /// attachments, platform, intake. Stops at the first failure.
        /// </summary>
        /// <param name="configuration">The application configuration (connection strings).</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>0 when every context is up to date afterwards, 1 otherwise.</returns>
        public static async Task<int> RunAsync(
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(loggerFactory);
            ILogger logger = loggerFactory.CreateLogger("Migrations");

            foreach (ContextTarget target in Targets)
            {
                string? connectionString = configuration.GetConnectionString(target.ConnectionName)
                    ?? configuration.GetConnectionString(DefaultConnectionName);
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    logger.LogError(
                        "No connection string for {Context}: set ConnectionStrings:{ConnectionName} (or ConnectionStrings:{Default} for every context)",
                        target.Name,
                        target.ConnectionName,
                        DefaultConnectionName);
                    return 1;
                }

                try
                {
                    await using DbContext context = target.Create(connectionString);
                    List<string> pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                    if (pending.Count == 0)
                    {
                        logger.LogInformation("{Context}: up to date", target.Name);
                        continue;
                    }

                    logger.LogInformation("{Context}: applying {Count} migration(s): {Migrations}", target.Name, pending.Count, string.Join(", ", pending));
                    await context.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("{Context}: done", target.Name);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The message names the failing context and migration; the
                    // connection string never reaches the log.
                    logger.LogError(ex, "{Context}: migration failed", target.Name);
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// The contexts in the order they are applied, with the same connection
        /// string keys and history tables Startup registers them with.
        /// </summary>
        private static readonly IReadOnlyList<ContextTarget> Targets =
        [
            new("forms", "FormsDb", cs => new FormsDbContext(
                new DbContextOptionsBuilder<FormsDbContext>().UseNpgsql(cs).Options)),
            new("attachments", "AttachmentsDb", cs => new AttachmentsDbContext(
                new DbContextOptionsBuilder<AttachmentsDbContext>()
                    .UseNpgsql(cs, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "attachments"))
                    .Options)),
            new("platform", "PlatformDb", cs => new PlatformDbContext(
                new DbContextOptionsBuilder<PlatformDbContext>()
                    .UseNpgsql(cs, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
                    .Options)),
            new("intake", "IntakeDb", cs => new IntakeDbContext(
                new DbContextOptionsBuilder<IntakeDbContext>()
                    .UseNpgsql(cs, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "intake"))
                    .Options)),
        ];

        private sealed record ContextTarget(string Name, string ConnectionName, Func<string, DbContext> Create);
    }
}
