namespace Myss.AdminApi.Tests.TestDoubles
{
    using Myss.AdminApi.Providers;
    using Myss.Common.Content;

    /// <summary>
    /// Fake <see cref="ISubmissionAcknowledgementAdminProvider"/> that keeps its entries in
    /// memory, newest first, records each create, and can be set to fail.
    /// </summary>
    public sealed class FakeSubmissionAcknowledgementAdminProvider : ISubmissionAcknowledgementAdminProvider
    {
        // A summer date, so the time reads the same under every time zone database.
        private static readonly DateTimeOffset FirstSave = new(2026, 7, 14, 17, 0, 0, TimeSpan.Zero);

        private int _saves;

        /// <summary>Gets the stored entries, newest first.</summary>
        public List<SubmissionAcknowledgementEntry> Entries { get; } = [];

        /// <summary>Gets every create, in call order.</summary>
        public List<(string FormSpecId, SubmissionAcknowledgementTexts Texts, string ChangedBy)> Creates { get; } = [];

        /// <summary>Gets or sets an exception for <see cref="ReadNewestAsync"/> to throw.</summary>
        public Exception? ReadException { get; set; }

        /// <summary>Gets or sets an exception for <see cref="CreateAsync"/> to throw.</summary>
        public Exception? CreateException { get; set; }

        /// <summary>Gets the cancellation token the last create was given.</summary>
        public CancellationToken LastCreateCancellationToken { get; private set; }

        /// <summary>Stores an entry as if someone had saved it earlier.</summary>
        /// <param name="changedBy">The IDIR username it records.</param>
        /// <returns>The stored entry.</returns>
        public SubmissionAcknowledgementEntry Seed(string changedBy = "KLEE")
        {
            return Store(new SubmissionAcknowledgementTexts("Seeded heading", "Seeded title", "Seeded {referenceNumber}"), changedBy);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<SubmissionAcknowledgementEntry>> ReadNewestAsync(
            string formSpecId,
            int count,
            CancellationToken cancellationToken)
        {
            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Task.FromResult<IReadOnlyList<SubmissionAcknowledgementEntry>>([.. Entries.Take(count)]);
        }

        /// <inheritdoc/>
        public Task<SubmissionAcknowledgementEntry> CreateAsync(
            string formSpecId,
            SubmissionAcknowledgementTexts texts,
            string changedBy,
            CancellationToken cancellationToken)
        {
            Creates.Add((formSpecId, texts, changedBy));
            LastCreateCancellationToken = cancellationToken;
            if (CreateException is not null)
            {
                throw CreateException;
            }

            return Task.FromResult(Store(texts, changedBy));
        }

        private SubmissionAcknowledgementEntry Store(SubmissionAcknowledgementTexts texts, string changedBy)
        {
            _saves++;
            var entry = new SubmissionAcknowledgementEntry
            {
                Id = $"doc-{_saves}",
                Heading = texts.Heading,
                Title = texts.Title,
                Body = texts.Body,
                ChangedBy = changedBy,
                ChangedAt = FirstSave.AddMinutes(_saves),
            };
            Entries.Insert(0, entry);
            return entry;
        }
    }
}
