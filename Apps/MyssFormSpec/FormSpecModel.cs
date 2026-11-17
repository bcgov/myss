namespace Myss.FormSpec
{
    using System.Text.Json;

    /// <summary>
    /// A versioned Form.io specification served from the content engine.
    /// </summary>
    public class FormSpecModel
    {
        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the spec version.
        /// </summary>
        public required int Version { get; set; }

        /// <summary>
        /// Gets or sets the human-readable title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the Form.io specification JSON.
        /// </summary>
        public required JsonElement Spec { get; set; }
    }
}
