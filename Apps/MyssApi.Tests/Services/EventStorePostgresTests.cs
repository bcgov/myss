namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Platform;
    using Myss.Api.Tests.TestDoubles;
    using Myss.Api.Tests.TestSupport;
    using Npgsql;

    /// <summary>
    /// The one guarantee the in-memory suite cannot prove: that Postgres, not
    /// the application, rejects the second of two appends that both passed the
    /// store's early check. This is what holds across API replicas.
    /// </summary>
    public class EventStorePostgresTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        private static readonly JsonElement Payload = JsonDocument.Parse("""{"eventVersion":1}""").RootElement;

        [PostgresFact]
        public async Task ThePrimaryKey_RejectsTheLoserOfARace()
        {
            Guid stream = Guid.NewGuid();
            DbContextOptions<PlatformDbContext> options = Options();
            using (var setup = new PlatformDbContext(options))
            {
                await setup.Database.MigrateAsync();
            }

            try
            {
                using var racing = new RacingPlatformDbContext(options, stream);
                var store = new EventStore(racing, new FakeCorrelationIdAccessor("req-pg"), new FakeTimeProvider(Now));

                ConcurrencyException ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
                    store.AppendAsync(stream, 0, [new DomainEvent("Submitted", Payload)], "loser", CancellationToken.None));

                Assert.Null(ex.CurrentVersion);
                var pg = Assert.IsType<PostgresException>(ex.InnerException?.InnerException);
                Assert.Equal(PostgresErrorCodes.UniqueViolation, pg.SqlState);

                using var check = new PlatformDbContext(options);
                Event survivor = Assert.Single(await check.Events.Where(e => e.StreamId == stream).ToListAsync());
                Assert.Equal("winner", survivor.Actor);
                Assert.Equal(1, survivor.Payload.RootElement.GetProperty("eventVersion").GetInt32());
            }
            finally
            {
                using var cleanup = new PlatformDbContext(options);
                await cleanup.Events.Where(e => e.StreamId == stream).ExecuteDeleteAsync();
            }
        }

        private static DbContextOptions<PlatformDbContext> Options()
        {
            return new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(
                    PostgresFactAttribute.ConnectionString,
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "platform"))
                .Options;
        }

        /// <summary>
        /// Lets a competing append win between the store's early check and its
        /// save, over the real provider: no translation, the constraint speaks.
        /// </summary>
        private sealed class RacingPlatformDbContext : PlatformDbContext
        {
            private readonly DbContextOptions<PlatformDbContext> _options;
            private readonly Guid _stream;
            private bool _raced;

            public RacingPlatformDbContext(DbContextOptions<PlatformDbContext> options, Guid stream)
                : base(options)
            {
                _options = options;
                _stream = stream;
            }

            public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                if (!_raced)
                {
                    _raced = true;
                    using var competitor = new PlatformDbContext(_options);
                    competitor.Events.Add(new Event
                    {
                        StreamId = _stream,
                        Version = 1,
                        Type = "Submitted",
                        Payload = JsonDocument.Parse("""{"eventVersion":1}"""),
                        Actor = "winner",
                        OccurredAt = Now,
                    });
                    await competitor.SaveChangesAsync(cancellationToken);
                }

                return await base.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
