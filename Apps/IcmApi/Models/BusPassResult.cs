namespace Icm.Api.Models
{
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// What the bus pass workflow said about a submission.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A populated <see cref="ErrorCode"/> is a business rejection carried on an HTTP
    /// 200</b>, so callers must check it — the repository cannot turn it into an
    /// exception, because the workflow's status vocabulary is undocumented and treating an
    /// unknown value as fatal would be a guess in the dangerous direction. Once a live
    /// call establishes the vocabulary, promote it here.
    /// </para>
    /// <para>
    /// <see cref="FirstName"/> and <see cref="LastName"/> echo what ICM matched — useful
    /// for server-side reconciliation and diagnostics (a match against a different name
    /// than the one submitted is how a mis-keyed SIN shows itself), and <b>never for
    /// display to the citizen</b>: echoing the matched name to whoever typed the SIN
    /// would let anyone confirm another person's name from their SIN. Surface only a
    /// generic identity-mismatch result.
    /// </para>
    /// </remarks>
    public class BusPassResult
    {
        /// <summary>Gets the application number ICM assigned.</summary>
        public string? ApplicationNumber { get; init; }

        /// <summary>Gets the error code; null or empty when the workflow reported none.</summary>
        public string? ErrorCode { get; init; }

        /// <summary>Gets the error message accompanying <see cref="ErrorCode"/>.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Gets the first name ICM echoed back.</summary>
        public string? FirstName { get; init; }

        /// <summary>Gets the last name ICM echoed back.</summary>
        public string? LastName { get; init; }

        /// <summary>Gets the workflow's status word (vocabulary unconfirmed).</summary>
        public string? Status { get; init; }

        /// <summary>
        /// Gets every field the workflow returned that this type has no property for, as
        /// the raw JSON that arrived.
        /// </summary>
        public IReadOnlyDictionary<string, JsonElement> AdditionalFields { get; init; } =
            new Dictionary<string, JsonElement>();
    }
}
