namespace Myss.Api.Tests.TestDoubles
{
    using Myss.Data.Platform;

    /// <summary>
    /// The real event store with a pause on either side of an append, so a
    /// test can hold a request inside its write sequence (after its row
    /// write, before or after its event) while another request runs.
    /// </summary>
    public sealed class PausableEventStore : IEventStore
    {
        private readonly IEventStore _inner;
        private readonly OneShotPause _beforeAppend;
        private readonly OneShotPause _afterAppend;

        /// <summary>Initializes a new instance of the <see cref="PausableEventStore"/> class.</summary>
        /// <param name="inner">The store that does the work.</param>
        /// <param name="beforeAppend">Held before the next append reaches the store.</param>
        /// <param name="afterAppend">Held after the next append has been written.</param>
        public PausableEventStore(IEventStore inner, OneShotPause beforeAppend, OneShotPause afterAppend)
        {
            _inner = inner;
            _beforeAppend = beforeAppend;
            _afterAppend = afterAppend;
        }

        /// <inheritdoc/>
        public async Task<int> AppendAsync(
            Guid streamId,
            int expectedVersion,
            IReadOnlyList<DomainEvent> events,
            string actor,
            CancellationToken cancellationToken)
        {
            await _beforeAppend.WaitIfArmedAsync(cancellationToken);
            int version = await _inner.AppendAsync(streamId, expectedVersion, events, actor, cancellationToken);
            await _afterAppend.WaitIfArmedAsync(cancellationToken);
            return version;
        }

        /// <inheritdoc/>
        public Task<EventStream> LoadAsync(Guid streamId, CancellationToken cancellationToken)
        {
            return _inner.LoadAsync(streamId, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyDictionary<Guid, EventStream>> LoadManyAsync(
            IReadOnlyCollection<Guid> streamIds,
            CancellationToken cancellationToken)
        {
            return _inner.LoadManyAsync(streamIds, cancellationToken);
        }
    }
}
