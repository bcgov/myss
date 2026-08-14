namespace Myss.Api.Models
{
    using System;
    using System.Text.Json.Serialization;
    using Myss.Api.Data;

    /// <summary>
    /// A stored attachment's metadata. The API doesn't return file content in
    /// this story; the id is what a submission will reference later.
    /// </summary>
    public class AttachmentResponseModel
    {
        /// <summary>
        /// Gets or sets the attachment identifier.
        /// </summary>
        public required Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the original filename, for display.
        /// </summary>
        public required string FileName { get; set; }

        /// <summary>
        /// Gets or sets the content type.
        /// </summary>
        public required string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the content size in bytes.
        /// </summary>
        public required long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the scan state. Always Released for now (scanning is
        /// synchronous), but kept on the wire so a future async pipeline can
        /// return quarantined attachments without a contract change.
        /// </summary>
        [JsonConverter(typeof(JsonStringEnumConverter<AttachmentStatus>))]
        public required AttachmentStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the form submission the attachment is attached to,
        /// when it has been attached.
        /// </summary>
        public Guid? SubmissionId { get; set; }

        /// <summary>
        /// Gets or sets the upload timestamp.
        /// </summary>
        public required DateTimeOffset UploadedAt { get; set; }
    }

    /// <summary>
    /// Why an attachment was not accepted.
    /// </summary>
    public enum AttachmentRejectionReason
    {
        /// <summary>The file was empty.</summary>
        Empty,

        /// <summary>The file exceeds the configured size cap.</summary>
        TooLarge,

        /// <summary>The declared content type is not accepted, or the bytes do not match it.</summary>
        TypeNotAllowed,

        /// <summary>The virus scan flagged the content.</summary>
        Infected,
    }

    /// <summary>
    /// Stable dotted error keywords for the attachments module. The keyword is
    /// the contract — the frontend matches on it and it doubles as a content
    /// key for the user-facing message. The ProblemDetails text is just for
    /// humans reading the response.
    /// </summary>
    public static class AttachmentErrorKeywords
    {
        /// <summary>No multipart file field was sent.</summary>
        public const string FileMissing = "DOC.UPLOAD.FILE_MISSING";

        /// <summary>No scan verdict could be obtained; the file was not stored.</summary>
        public const string ScanUnavailable = "DOC.SCAN.UNAVAILABLE";

        /// <summary>
        /// Maps a rejection reason to its keyword.
        /// </summary>
        /// <param name="reason">The rejection reason.</param>
        /// <returns>The stable dotted keyword.</returns>
        public static string ToKeyword(this AttachmentRejectionReason reason) => reason switch
        {
            AttachmentRejectionReason.Empty => "DOC.UPLOAD.EMPTY",
            AttachmentRejectionReason.TooLarge => "DOC.UPLOAD.TOO_LARGE",
            AttachmentRejectionReason.TypeNotAllowed => "DOC.UPLOAD.TYPE_NOT_ALLOWED",
            AttachmentRejectionReason.Infected => "DOC.UPLOAD.INFECTED",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
        };
    }

    /// <summary>
    /// The outcome of an upload attempt: either the stored attachment, or a
    /// rejection reason.
    /// </summary>
    public class AttachmentUploadResult
    {
        /// <summary>
        /// Gets or sets the stored attachment. Null when rejected.
        /// </summary>
        public AttachmentResponseModel? Attachment { get; set; }

        /// <summary>
        /// Gets or sets the rejection reason. Null when accepted.
        /// </summary>
        public AttachmentRejectionReason? Rejection { get; set; }

        /// <summary>
        /// Gets or sets a human-readable rejection detail. For an infected
        /// file this names the matched signature; it never echoes file content.
        /// </summary>
        public string? Detail { get; set; }

        /// <summary>
        /// Creates an accepted outcome.
        /// </summary>
        /// <param name="attachment">The stored attachment.</param>
        /// <returns>The outcome.</returns>
        public static AttachmentUploadResult Accepted(AttachmentResponseModel attachment) =>
            new() { Attachment = attachment };

        /// <summary>
        /// Creates a rejected outcome.
        /// </summary>
        /// <param name="reason">The rejection reason.</param>
        /// <param name="detail">The human-readable detail.</param>
        /// <returns>The outcome.</returns>
        public static AttachmentUploadResult Rejected(AttachmentRejectionReason reason, string detail) =>
            new() { Rejection = reason, Detail = detail };
    }
}
