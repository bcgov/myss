namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Microsoft.EntityFrameworkCore;
    using Myss.Api.Platform;
    using Myss.Api.Tests.TestDoubles;

    /// <summary>
    /// The store's two guards against a lost update: the early version check,
    /// and the (stream, version) key when two appends pass the check together.
    /// </summary>
    public class EventStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        private static readonly JsonElement Payload = JsonDocument.Parse("""{"eventVersion":1}""").RootElement;

        private readonly string _dbName = Guid.NewGuid().ToString();
        private readonly FakeCorrelationIdAccessor _correlation = new("req-42");
        private readonly FakeTimeProvider _clock = new(Now);

        [Fact]
        public async Task AppendingToAnEmptyStream_LandsAtVersionOneWithTheBookkeeping()
        {
            Guid stream = Guid.NewGuid();
            using PlatformDbContext db = NewContext();
            EventStore store = NewStore(db);

            int version = await store.AppendAsync(stream, 0, [new DomainEvent("Submitted", Payload)], "applicant:mock-alice", CancellationToken.None);
            EventStream loaded = await store.LoadAsync(stream, CancellationToken.None);

            Assert.Equal(1, version);
            Assert.Equal(1, loaded.Version);
            StoredEvent evt = Assert.Single(loaded.Events);
            Assert.Equal("Submitted", evt.Type);
            Assert.Equal("applicant:mock-alice", evt.Actor);
            Assert.Equal(Now, evt.OccurredAt);
            Assert.Equal("req-42", (await db.Events.SingleAsync()).RequestId);
        }

        [Fact]
        public async Task AppendingWithAStaleExpectedVersion_FailsEarlyWithTheCurrentVersion()
        {
            Guid stream = Guid.NewGuid();
            using PlatformDbContext db = NewContext();
            EventStore store = NewStore(db);
            await store.AppendAsync(stream, 0, [new DomainEvent("Submitted", Payload)], "a", CancellationToken.None);

            ConcurrencyException ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
                store.AppendAsync(stream, 0, [new DomainEvent("Submitted", Payload)], "a", CancellationToken.None));

            Assert.Equal(1, ex.CurrentVersion);
            Assert.Equal(0, ex.ExpectedVersion);
            Assert.Equal(1, (await store.LoadAsync(stream, CancellationToken.None)).Version);
        }

        [Fact]
        public async Task TwoAppendsThatBothPassTheEarlyCheck_OnlyOneWins()
        {
            // The race the early check cannot see: both callers read version 0,
            // then both write. The key on (stream, version) decides, and the
            // loser gets a ConcurrencyException it can only answer by reloading.
            Guid stream = Guid.NewGuid();
            using RacingPlatformDbContext db = new(Options(), stream);
            EventStore store = NewStore(db);

            ConcurrencyException ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
                store.AppendAsync(stream, 0, [new DomainEvent("Submitted", Payload)], "loser", CancellationToken.None));

            Assert.Null(ex.CurrentVersion);
            using RacingPlatformDbContext check = new(Options(), stream: null);
            StoredEvent survivor = Assert.Single((await NewStore(check).LoadAsync(stream, CancellationToken.None)).Events);
            Assert.Equal("winner", survivor.Actor);
        }

        [Fact]
        public async Task LoadMany_ReturnsAStreamPerRequestedIdIncludingEmptyOnes()
        {
            Guid a = Guid.NewGuid();
            Guid b = Guid.NewGuid();
            Guid empty = Guid.NewGuid();
            using PlatformDbContext db = NewContext();
            EventStore store = NewStore(db);
            await store.AppendAsync(a, 0, [new DomainEvent("Submitted", Payload)], "a", CancellationToken.None);
            await store.AppendAsync(b, 0, [new DomainEvent("Submitted", Payload), new DomainEvent("PickedUp", Payload)], "b", CancellationToken.None);

            IReadOnlyDictionary<Guid, EventStream> streams = await store.LoadManyAsync([a, b, empty, a], CancellationToken.None);

            Assert.Equal(3, streams.Count);
            Assert.Equal(1, streams[a].Version);
            Assert.Equal(2, streams[b].Version);
            Assert.Equal(0, streams[empty].Version);
        }

        [Fact]
        public async Task AppendingNothing_IsRefused()
        {
            using PlatformDbContext db = NewContext();
            EventStore store = NewStore(db);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.AppendAsync(Guid.NewGuid(), 0, [], "a", CancellationToken.None));
        }

        private DbContextOptions<PlatformDbContext> Options()
        {
            return new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
        }

        private PlatformDbContext NewContext() => new InMemoryPlatformDbContext(Options());

        private EventStore NewStore(PlatformDbContext db) => new(db, _correlation, _clock);

        /// <summary>
        /// A context that lets a competing append win between the store's
        /// early check and its own save: exactly the window the database key
        /// exists for.
        /// </summary>
        private sealed class RacingPlatformDbContext : InMemoryPlatformDbContext
        {
            private readonly DbContextOptions<PlatformDbContext> _options;
            private readonly Guid? _stream;
            private bool _raced;

            // A null stream means "do not race": the competitor is this same
            // context type so both share one EF model, which the InMemory
            // provider requires of everything using one database name.
            public RacingPlatformDbContext(DbContextOptions<PlatformDbContext> options, Guid? stream)
                : base(options)
            {
                _options = options;
                _stream = stream;
            }

            public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                if (_stream is Guid stream && !_raced)
                {
                    _raced = true;
                    using var competitor = new RacingPlatformDbContext(_options, stream: null);
                    competitor.Events.Add(new EventRow
                    {
                        StreamId = stream,
                        Version = 1,
                        Type = "Submitted",
                        Payload = JsonDocument.Parse("""{"eventVersion":1}"""),
                        Actor = "winner",
                        OccurredAt = Now,
                    });
                    await competitor.SaveChangesAsync(cancellationToken);
                }

                try
                {
                    return await base.SaveChangesAsync(cancellationToken);
                }
                catch (ArgumentException ex) when (_stream is not null)
                {
                    // The InMemory provider surfaces a duplicate key as a bare
                    // ArgumentException; a relational provider wraps the
                    // constraint violation in DbUpdateException, which is what
                    // the store maps. Translate so the store's path is the one
                    // under test. The Postgres-backed CI run proves the real one.
                    throw new DbUpdateException("Duplicate key (simulated relational provider).", ex);
                }
            }
        }
    }
}
