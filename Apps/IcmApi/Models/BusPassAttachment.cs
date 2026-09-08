namespace Icm.Api.Models
{
    using System;

    /// <summary>
    /// A file to attach to the bus pass submission.
    /// </summary>
    /// <remarks>
    /// The retired MySS SOAP path sent no attachment, but the workflow's operation name
    /// (<c>…FromAdobe</c>) suggests the original channel attached a rendered form, and the
    /// spec hints the list may be mandatory (<c>minItems: 1</c>). Kept optional here until
    /// a live call settles it.
    /// </remarks>
    public class BusPassAttachment
    {
        /// <summary>Gets or sets the file name, including its extension.</summary>
        public string? FileName { get; set; }

        /// <summary>Gets or sets the file content. Base64-encoded on the wire.</summary>
        public ReadOnlyMemory<byte> Content { get; set; }
    }
}
