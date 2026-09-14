namespace Icm.Api.Workflows.Contracts
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The transaction header of a bus pass workflow message.
    /// </summary>
    /// <remarks>
    /// Field-for-field the generic header the retired SOAP integration's
    /// <c>ICMClient.SetGenericHeader</c> filled (per the INT-316 field-mapping analysis),
    /// plus <c>WMInstanceId</c>. The mapper supplies the identity fields and leaves the
    /// bookkeeping slots null — the old integration sent those as empty strings, but
    /// MEASURED SIT2 2026-09-10, submissions succeed identically without them. Nothing
    /// here comes from the caller.
    /// </remarks>
    internal class SiebelBusPassHeader
    {
        /// <summary>
        /// Gets or sets <c>TransactionName</c> — <c>INT-316</c>, the name of MySS's bus
        /// pass integration (the caller), not of the workflow being called.
        /// </summary>
        [JsonPropertyName("TransactionName")]
        public string? TransactionName { get; set; }

        /// <summary>Gets or sets <c>WMInstanceId</c> (not part of the old SOAP header; not sent).</summary>
        [JsonPropertyName("WMInstanceId")]
        public string? WMInstanceId { get; set; }

        /// <summary>Gets or sets <c>SourceReference</c> (not sent).</summary>
        [JsonPropertyName("SourceReference")]
        public string? SourceReference { get; set; }

        /// <summary>Gets or sets <c>TargetReference</c> (not sent).</summary>
        [JsonPropertyName("TargetReference")]
        public string? TargetReference { get; set; }

        /// <summary>Gets or sets <c>UserId</c>.</summary>
        [JsonPropertyName("UserId")]
        public string? UserId { get; set; }

        /// <summary>Gets or sets <c>SourceSystem</c>.</summary>
        [JsonPropertyName("SourceSystem")]
        public string? SourceSystem { get; set; }

        /// <summary>Gets or sets <c>TargetSystem</c> — <c>ICM</c>.</summary>
        [JsonPropertyName("TargetSystem")]
        public string? TargetSystem { get; set; }

        /// <summary>Gets or sets <c>Timestamp</c>, formatted <c>yyyyMMddTHHmmssZ</c> in UTC.</summary>
        [JsonPropertyName("Timestamp")]
        public string? Timestamp { get; set; }

        /// <summary>Gets or sets <c>Status</c> — the old integration sent <c>SUCCESS</c> outbound.</summary>
        [JsonPropertyName("Status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets <c>ErrorCode</c> (not sent outbound).</summary>
        [JsonPropertyName("ErrorCode")]
        public string? ErrorCode { get; set; }

        /// <summary>Gets or sets <c>ErrorMessage</c> (not sent outbound).</summary>
        [JsonPropertyName("ErrorMessage")]
        public string? ErrorMessage { get; set; }

        /// <summary>Gets or sets <c>Attribute1</c> (not sent outbound).</summary>
        [JsonPropertyName("Attribute1")]
        public string? Attribute1 { get; set; }

        /// <summary>Gets or sets <c>Attribute2</c> (not sent outbound).</summary>
        [JsonPropertyName("Attribute2")]
        public string? Attribute2 { get; set; }

        /// <summary>Gets or sets <c>Attribute3</c> (not sent outbound).</summary>
        [JsonPropertyName("Attribute3")]
        public string? Attribute3 { get; set; }

        /// <summary>Gets or sets <c>Attribute4</c> (not sent outbound).</summary>
        [JsonPropertyName("Attribute4")]
        public string? Attribute4 { get; set; }

        /// <summary>Gets or sets <c>Attribute5</c> (not sent outbound).</summary>
        [JsonPropertyName("Attribute5")]
        public string? Attribute5 { get; set; }
    }
}
