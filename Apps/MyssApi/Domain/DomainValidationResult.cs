namespace Myss.Api.Domain
{
    /// <summary>
    /// The outcome of validating one value: either a normalised value, or a
    /// keyword explaining the refusal.
    /// </summary>
    /// <remarks>
    /// Returned rather than thrown. A submission produces a collection of
    /// failures, not the first one — a citizen should see every problem on the
    /// page at once, not be walked through them one reload at a time.
    /// </remarks>
    /// <typeparam name="T">The normalised value type.</typeparam>
    public readonly struct DomainValidationResult<T>
    {
        private DomainValidationResult(bool isValid, T? value, string? keyword, string? message)
        {
            IsValid = isValid;
            Value = value;
            Keyword = keyword;
            Message = message;
        }

        /// <summary>Gets a value indicating whether the input was accepted.</summary>
        public bool IsValid { get; }

        /// <summary>Gets the normalised value. Meaningful only when valid.</summary>
        public T? Value { get; }

        /// <summary>Gets the stable failure keyword. Null when valid.</summary>
        public string? Keyword { get; }

        /// <summary>Gets the human-readable failure message. Null when valid.</summary>
        public string? Message { get; }

        /// <summary>Creates a successful result carrying the normalised value.</summary>
        /// <param name="value">The normalised value.</param>
        /// <returns>A valid result.</returns>
        public static DomainValidationResult<T> Ok(T value) => new(true, value, null, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="keyword">The stable keyword.</param>
        /// <param name="message">The human-readable message.</param>
        /// <returns>An invalid result.</returns>
        public static DomainValidationResult<T> Fail(string keyword, string message) =>
            new(false, default, keyword, message);
    }
}
