namespace Myss.Api.Tests.Configuration
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Configuration;
    using Myss.Api.Data;
    using Myss.Api.Intake;
    using Myss.Api.Platform;
    using Myss.Api.Tests.TestSupport;
    using Npgsql;

    /// <summary>
    /// The migrate mode the deployment pipeline runs as a Job.
    /// </summary>
    public class MigrationRunnerTests
    {
        [Fact]
        public void TheSwitch_IsRecognisedAndStripped()
        {
            string[] args = ["--urls", "http://*:8080", MigrationRunner.Switch];

            Assert.True(MigrationRunner.IsRequested(args));
            Assert.False(MigrationRunner.IsRequested(["--urls", "http://*:8080"]));
            Assert.Equal(["--urls", "http://*:8080"], MigrationRunner.WithoutSwitch(args));
        }

        [Fact]
        public void TheEnvironmentVariable_SelectsTheModeToo()
        {
            // The deployment Job cannot pass args through the S2I run script,
            // so it sets this instead.
            string? before = Environment.GetEnvironmentVariable(MigrationRunner.EnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(MigrationRunner.EnvironmentVariable, "true");
                Assert.True(MigrationRunner.IsRequested([]));
                Environment.SetEnvironmentVariable(MigrationRunner.EnvironmentVariable, "false");
                Assert.False(MigrationRunner.IsRequested([]));
                Environment.SetEnvironmentVariable(MigrationRunner.EnvironmentVariable, "yes");
                Assert.False(MigrationRunner.IsRequested([]));
            }
            finally
            {
                Environment.SetEnvironmentVariable(MigrationRunner.EnvironmentVariable, before);
            }
        }

        [Fact]
        public async Task WithoutAConnectionString_ItRefusesBeforeTouchingAnything()
        {
            IConfiguration empty = new ConfigurationBuilder().Build();

            int exitCode = await MigrationRunner.RunAsync(empty, NullLoggerFactory.Instance, CancellationToken.None);

            Assert.Equal(1, exitCode);
        }

        [PostgresFact]
        public async Task AgainstARealDatabase_EveryContextEndsUpToDateAndARerunIsANoOp()
        {
            string connectionString = PostgresFactAttribute.ConnectionString!;
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:FormsDb"] = connectionString,
                })
                .Build();

            int first = await MigrationRunner.RunAsync(configuration, NullLoggerFactory.Instance, CancellationToken.None);
            int second = await MigrationRunner.RunAsync(configuration, NullLoggerFactory.Instance, CancellationToken.None);

            Assert.Equal(0, first);
            Assert.Equal(0, second);
            await AssertUpToDate(new FormsDbContext(new DbContextOptionsBuilder<FormsDbContext>().UseNpgsql(connectionString).Options));
            await AssertUpToDate(new AttachmentsDbContext(new DbContextOptionsBuilder<AttachmentsDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "attachments")).Options));
            await AssertUpToDate(new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "platform")).Options));
            await AssertUpToDate(new IntakeDbContext(new DbContextOptionsBuilder<IntakeDbContext>()
                .UseNpgsql(connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "intake")).Options));
        }

        [PostgresFact]
        public async Task AnUnreachableDatabase_FailsWithExitCodeOne()
        {
            var builder = new NpgsqlConnectionStringBuilder(PostgresFactAttribute.ConnectionString!)
            {
                Port = 1,
                Timeout = 2,
            };
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:FormsDb"] = builder.ConnectionString,
                })
                .Build();

            int exitCode = await MigrationRunner.RunAsync(configuration, NullLoggerFactory.Instance, CancellationToken.None);

            Assert.Equal(1, exitCode);
        }

        private static async Task AssertUpToDate(DbContext context)
        {
            await using (context)
            {
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
                Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
            }
        }
    }
}
