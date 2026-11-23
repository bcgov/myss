namespace Myss.Data
{
    using System;

    /// <summary>
    /// A file uploaded through the attachments module and linked to an account
    /// at registration, with what it is.
    /// </summary>
    public class AccountDocument
    {
        /// <summary>Gets or sets the row identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the owning profile.</summary>
        public Guid ProfileId { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the file's row in the attachments
        /// schema. There is no foreign key: that schema belongs to another
        /// context, which may use another database.
        /// </summary>
        public Guid AttachmentId { get; set; }

        /// <summary>Gets or sets what the document is.</summary>
        public AccountDocumentPurpose Purpose { get; set; }

        /// <summary>Gets or sets when the document was linked to the account.</summary>
        public DateTimeOffset AttachedAt { get; set; }
    }
}
