namespace Myss.Common.Content
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The form messages MySS Administration can edit. A message is added here when a
    /// form's page starts reading it; both APIs refuse any other form ID and key.
    /// </summary>
    public static class FormMessageDefinitions
    {
        /// <summary>Gets every defined message, in the order the admin site lists them.</summary>
        public static IReadOnlyList<FormMessageDefinition> All { get; } =
        [
            new(
                "bc-bus-pass",
                "confirmation",
                "Confirmation message",
                "Shown after a request is submitted successfully.",
                [new("referenceNumber", "the applicant's reference number", "1-0123456789", Required: true)]),
        ];

        /// <summary>Finds a defined message, comparing the form ID and key exactly.</summary>
        /// <param name="formSpecId">The form.</param>
        /// <param name="messageKey">The message's key.</param>
        /// <returns>The definition, or null when the form has no such message.</returns>
        public static FormMessageDefinition? Find(string formSpecId, string messageKey) =>
            All.FirstOrDefault(definition =>
                string.Equals(definition.FormSpecId, formSpecId, StringComparison.Ordinal)
                && string.Equals(definition.MessageKey, messageKey, StringComparison.Ordinal));
    }
}
