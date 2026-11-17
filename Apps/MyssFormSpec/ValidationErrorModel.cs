namespace Myss.FormSpec
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One rejected value: which field, why, and what to tell the citizen.
    /// </summary>
    /// <remarks>
    /// The shape is fixed by §7.2 of the assessment: <c>{ field, keyword,
    /// message }</c>. <c>field</c> lets the client build the WCAG-required
    /// error summary whose entries link to and focus the errored field;
    /// <c>keyword</c> is stable across wording changes and translation.
    /// </remarks>
    public class ValidationErrorModel
    {
        /// <summary>Gets or sets the component key the failure belongs to.</summary>
        public required string Field { get; set; }

        /// <summary>Gets or sets the stable failure keyword.</summary>
        public required string Keyword { get; set; }

        /// <summary>Gets or sets the human-readable message.</summary>
        public required string Message { get; set; }

        /// <summary>
        /// Gets a value indicating whether the message was authored on the form
        /// itself (the component's <c>errors</c> map or
        /// <c>validate.customMessage</c>). Such wording is specific to the field
        /// and outranks the catalogue, so the resolver leaves it alone. Not part
        /// of the wire shape.
        /// </summary>
        [JsonIgnore]
        public bool HasAuthoredMessage { get; init; }
    }
}
