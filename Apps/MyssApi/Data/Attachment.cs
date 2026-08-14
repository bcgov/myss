namespace Myss.Api.Data
{
    using System;

    /// <summary>
    /// Scan lifecycle of an attachment (quarantine -> scan -> release, same
    /// naming as the forms architecture). The row is created before the scan,
    /// so a crashed upload shows up as a stale quarantined row instead of an
    /// orphaned object in the bucket.
    /// </summary>
    public enum AttachmentStatus
    {
        /// <summary>Row created, no scan verdict yet. Nothing in the object store.</summary>
        Quarantined,

        /// <summary>Scanned clean and stored.</summary>
        Released,

        /// <summary>Flagged by the scan. Row kept for audit, content never stored.</summary>
        Rejected,
    }

    /// <summary>
    /// Metadata for an attachment. The file itself lives in the object store
    /// under <see cref="StorageKey"/> once the attachment is released.
    /// </summary>
    public class Attachment
    {
        /// <summary>
        /// Gets or sets the attachment identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the owner's Keycloak <c>sub</c> claim. Identity stays
        /// in Keycloak, so there is no local users table to join against.
        /// </summary>
        public required string OwnerSubject { get; set; }

        /// <summary>
        /// Gets or sets the original filename. Display only — it never becomes
        /// part of the storage key.
        /// </summary>
        public required string FileName { get; set; }

        /// <summary>
        /// Gets or sets the content type (declared by the client, then checked
        /// against the file's magic bytes).
        /// </summary>
        public required string ContentType { get; set; }

        /// <summary>
        /// Gets or sets the content size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the generated object-store key.
        /// </summary>
        public required string StorageKey { get; set; }

        /// <summary>
        /// Gets or sets the scan lifecycle state.
        /// </summary>
        public AttachmentStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the ETag the object store returned. Set when the file
        /// is stored, so null until released.
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// Gets or sets the signature the scan matched. Only set on rejected
        /// rows.
        /// </summary>
        public string? ScanSignature { get; set; }

        /// <summary>
        /// Gets or sets the form submission this file belongs to (future
        /// attach flow). Points into another module's schema, so it's a plain
        /// column rather than a real foreign key.
        /// </summary>
        public Guid? SubmissionId { get; set; }

        /// <summary>
        /// Gets or sets the upload timestamp.
        /// </summary>
        public DateTimeOffset UploadedAt { get; set; }
    }
}
