namespace Myss.AdminApi.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Myss.AdminApi.Domain;
    using Myss.AdminApi.Models;
    using Myss.Common.Content;
    using Myss.FormSpec;

    /// <summary>
    /// Validates a form message save before anything is written. Pure: the message's
    /// definition and the request in, every problem out, each on its field
    /// (<c>heading</c>, <c>title</c>, <c>body</c>). Lengths are counted after
    /// <see cref="Normalise"/>; a message over its limit is not checked further.
    /// </summary>
    /// <remarks>
    /// Mirrors MyssWebCommon's <c>validateFormMessage</c>: the same limits, keywords
    /// and wording.
    /// </remarks>
    public static partial class FormMessageValidator
    {
        private static readonly TextField Heading = new("heading", "The page heading", "Enter a page heading.", 100);
        private static readonly TextField Title = new("title", "The message title", "Enter a message title.", 100);
        private static readonly TextField Body = new("body", "The message", "Enter the message.", 1000);

        /// <summary>
        /// The text as it is checked and stored: every line break a single line feed,
        /// and no spaces at either end.
        /// </summary>
        /// <param name="text">The text as typed.</param>
        /// <returns>The normalised text; empty for null.</returns>
        public static string Normalise(string? text) =>
            (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

        /// <summary>Validates a save request against the rules and the message's placeholders.</summary>
        /// <param name="definition">The message being saved.</param>
        /// <param name="request">The texts to save.</param>
        /// <returns>Every problem found, field by field. Empty when the message can be saved.</returns>
        public static IReadOnlyList<ValidationErrorModel> Validate(
            FormMessageDefinition definition,
            SaveFormMessageRequestModel request)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(request);

            string body = Normalise(request.Body);
            List<ValidationErrorModel> errors =
            [
                .. TextErrors(Heading, Normalise(request.Heading)),
                .. TextErrors(Title, Normalise(request.Title)),
                .. TextErrors(Body, body),
            ];
            if (body.Length > 0 && body.Length <= Body.MaxLength)
            {
                errors.AddRange(PlaceholderErrors(definition.Placeholders, body));
                errors.AddRange(LinkErrors(body));
            }

            return errors;
        }

        private static IEnumerable<ValidationErrorModel> TextErrors(TextField field, string text)
        {
            if (text.Length == 0)
            {
                yield return Error(field.Name, FormMessageKeywords.TextRequired, field.RequiredMessage);
            }
            else if (text.Length > field.MaxLength)
            {
                yield return Error(
                    field.Name,
                    FormMessageKeywords.TextTooLong,
                    $"{field.Label} must be {Count(field.MaxLength)} characters or fewer. It has {Count(text.Length)}.");
            }
        }

        private static IEnumerable<ValidationErrorModel> PlaceholderErrors(
            IReadOnlyList<FormMessagePlaceholder> placeholders,
            string body)
        {
            // A placeholder inside a link is not replaced, so it does not count.
            string outsideLinks = LinkPattern().Replace(body, string.Empty);
            foreach (FormMessagePlaceholder placeholder in placeholders.Where(placeholder => placeholder.Required))
            {
                string braced = Braced(placeholder.Name);
                if (!outsideLinks.Contains(braced, StringComparison.Ordinal))
                {
                    yield return Error(
                        "body",
                        FormMessageKeywords.PlaceholderMissing,
                        $"Add {braced} where {placeholder.Description} should appear.");
                }
            }

            List<string> allowed = [.. placeholders.Select(placeholder => Braced(placeholder.Name))];
            IEnumerable<string> unknown = PlaceholderPattern().Matches(body)
                .Select(match => match.Value)
                .Where(found => !allowed.Contains(found, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal);
            foreach (string found in unknown)
            {
                yield return Error(
                    "body",
                    FormMessageKeywords.PlaceholderUnknown,
                    $"{found} is not a placeholder this message can use.{AllowedSentence(allowed)}");
            }
        }

        private static IEnumerable<ValidationErrorModel> LinkErrors(string body) =>
            LinkPattern().Matches(body)
                .Where(link => !SafeLinkTarget().IsMatch(link.Groups["target"].Value))
                .Select(link => Error(
                    "body",
                    FormMessageKeywords.LinkInvalid,
                    $"The link “{link.Groups["text"].Value}” must start with https://, mailto: or tel:, with no spaces."));

        private static string AllowedSentence(List<string> allowed) => allowed.Count switch
        {
            0 => string.Empty,
            1 => $" The only one is {allowed[0]}.",
            _ => $" The ones it can use are {string.Join(", ", allowed[..^1])} and {allowed[^1]}.",
        };

        private static string Braced(string name) => "{" + name + "}";

        private static string Count(int number) => number.ToString("N0", CultureInfo.InvariantCulture);

        private static ValidationErrorModel Error(string field, string keyword, string message) =>
            new() { Field = field, Keyword = keyword, Message = message };

        [GeneratedRegex(@"\{[^{}\n]+\}", RegexOptions.CultureInvariant)]
        private static partial Regex PlaceholderPattern();

        [GeneratedRegex(@"\[(?<text>[^\]\n]+)\]\((?<target>[^)\n]*)\)", RegexOptions.CultureInvariant)]
        private static partial Regex LinkPattern();

        [GeneratedRegex(@"^(https://|mailto:|tel:)\S+$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
        private static partial Regex SafeLinkTarget();

        private sealed record TextField(string Name, string Label, string RequiredMessage, int MaxLength);
    }
}
