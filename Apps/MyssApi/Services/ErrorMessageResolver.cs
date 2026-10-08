namespace Myss.Api.Services
{
    using System.Collections.Generic;
    using Myss.Api.Models;

    /// <summary>
    /// Words a set of validation failures from the error message catalogue.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A validator emits a keyword with the compiled default text; this is the
    /// step that swaps in the wording a Service Designer published, keyword by
    /// keyword, before a 422 leaves the API. It is pure (errors in, catalogue in,
    /// errors out) so the validators never learn about the content engine and
    /// the overlay can be tested without one.
    /// </para>
    /// <para>
    /// Wording authored on the form itself (a component's <c>errors</c> map or
    /// <c>validate.customMessage</c>, flagged by
    /// <see cref="ValidationErrorModel.HasAuthoredMessage"/>) outranks the
    /// catalogue and is left alone: it is specific to that field, and the
    /// catalogue row is the generic wording for the keyword. A keyword the
    /// catalogue has no row for keeps the text the validator gave it, so the
    /// keywords whose message embeds a value stay in code (see the header of
    /// <c>Shared/validation/error-messages.json</c>).
    /// </para>
    /// </remarks>
    public static class ErrorMessageResolver
    {
        /// <summary>
        /// Returns the failures with each message replaced by the catalogue's
        /// wording for its keyword, where the catalogue has one and the form
        /// did not author its own.
        /// </summary>
        /// <param name="errors">The failures as the validators produced them.</param>
        /// <param name="catalogue">The wording keyed by keyword.</param>
        /// <returns>A new list in the same order; the input is not modified.</returns>
        public static IReadOnlyList<ValidationErrorModel> Resolve(
            IReadOnlyList<ValidationErrorModel> errors,
            IReadOnlyDictionary<string, string> catalogue)
        {
            var resolved = new List<ValidationErrorModel>(errors.Count);
            foreach (ValidationErrorModel error in errors)
            {
                resolved.Add(
                    !error.HasAuthoredMessage
                    && catalogue.TryGetValue(error.Keyword, out string? message)
                    && !string.IsNullOrWhiteSpace(message)
                        ? new ValidationErrorModel { Field = error.Field, Keyword = error.Keyword, Message = message }
                        : error);
            }

            return resolved;
        }
    }
}
