namespace Myss.AdminApi.Tests.TestDoubles
{
    using Myss.AdminApi.Providers;
    using Myss.Common.Content;

    /// <summary>
    /// Fake <see cref="IFormMessageAdminProvider"/> that keeps each message's entries in
    /// memory, newest first, records each create, and can be set to fail.
    /// </summary>
    public sealed class FakeFormMessageAdminProvider : IFormMessageAdminProvider
    {
        // A summer date, so the time reads the same under every time zone database.
        private static readonly DateTimeOffset FirstSave = new(2026, 7, 14, 17, 0, 0, TimeSpan.Zero);

        private readonly Dictionary<(string FormSpecId, string MessageKey), List<FormMessageEntry>> _entries = [];

        private int _saves;

        /// <summary>Gets every create, in call order.</summary>
        public List<(string FormSpecId, string MessageKey, FormMessageTexts Texts, string ChangedBy)> Creates { get; } = [];

        /// <summary>Gets every read, in call order.</summary>
        public List<(string FormSpecId, string MessageKey, int Count)> Reads { get; } = [];

        /// <summary>Gets or sets an exception for <see cref="ReadNewestAsync"/> to throw.</summary>
        public Exception? ReadException { get; set; }

        /// <summary>Gets or sets an exception for <see cref="CreateAsync"/> to throw.</summary>
        public Exception? CreateException { get; set; }

        /// <summary>Gets the cancellation token the last create was given.</summary>
        public CancellationToken LastCreateCancellationToken { get; private set; }

        /// <summary>Stores an entry as if someone had saved it earlier.</summary>
        /// <param name="changedBy">The IDIR username it records.</param>
        /// <param name="formSpecId">The form.</param>
        /// <param name="messageKey">The message's key within the form.</param>
        /// <returns>The stored entry.</returns>
        public FormMessageEntry Seed(
            string changedBy = "KLEE",
            string formSpecId = "bc-bus-pass",
            string messageKey = "confirmation")
        {
            return Store(
                formSpecId,
                messageKey,
                new FormMessageTexts("Seeded heading", "Seeded title", "Seeded {referenceNumber}"),
                changedBy);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<FormMessageEntry>> ReadNewestAsync(
            string formSpecId,
            string messageKey,
            int count,
            CancellationToken cancellationToken)
        {
            Reads.Add((formSpecId, messageKey, count));
            if (ReadException is not null)
            {
                throw ReadException;
            }

            return Task.FromResult<IReadOnlyList<FormMessageEntry>>([.. EntriesOf(formSpecId, messageKey).Take(count)]);
        }

        /// <inheritdoc/>
        public Task<FormMessageEntry> CreateAsync(
            string formSpecId,
            string messageKey,
            FormMessageTexts texts,
            string changedBy,
            CancellationToken cancellationToken)
        {
            Creates.Add((formSpecId, messageKey, texts, changedBy));
            LastCreateCancellationToken = cancellationToken;
            if (CreateException is not null)
            {
                throw CreateException;
            }

            return Task.FromResult(Store(formSpecId, messageKey, texts, changedBy));
        }

        private List<FormMessageEntry> EntriesOf(string formSpecId, string messageKey)
        {
            if (!_entries.TryGetValue((formSpecId, messageKey), out List<FormMessageEntry>? entries))
            {
                entries = [];
                _entries[(formSpecId, messageKey)] = entries;
            }

            return entries;
        }

        private FormMessageEntry Store(string formSpecId, string messageKey, FormMessageTexts texts, string changedBy)
        {
            _saves++;
            var entry = new FormMessageEntry
            {
                Id = $"doc-{_saves}",
                Heading = texts.Heading,
                Title = texts.Title,
                Body = texts.Body,
                ChangedBy = changedBy,
                ChangedAt = FirstSave.AddMinutes(_saves),
            };
            EntriesOf(formSpecId, messageKey).Insert(0, entry);
            return entry;
        }
    }
}
