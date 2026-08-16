namespace Myss.Api.Configuration.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Attachment acceptance rules, bound from the <c>Attachments</c>
    /// configuration section.
    /// </summary>
    public class AttachmentsConfig
    {
        /// <summary>
        /// Gets or sets the maximum accepted file size in bytes. Default is
        /// the forms architecture's per-file cap (5 MB); per-submission caps
        /// come later with the attach flow. Keep this at or below clamd's
        /// <c>StreamMaxLength</c> (100M in the gitops manifest).
        /// </summary>
        public long MaxSizeBytes { get; set; } = 5_242_880; // 5 MiB

        /// <summary>
        /// Gets or sets the accepted content types. Each entry needs a
        /// magic-byte check in the service — an allowed type without one gets
        /// rejected, not waved through.
        /// </summary>
        public IReadOnlyList<string> AllowedContentTypes { get; set; } =
            ["application/pdf", "image/png", "image/jpeg"];
    }
}
