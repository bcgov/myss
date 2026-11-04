namespace Myss.Common.Content
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The forms whose page shows a confirmation message read from the content
    /// engine. Matches the list in MyssWebCommon's message rules.
    /// </summary>
    public static class SubmissionAcknowledgementForms
    {
        /// <summary>Gets the form IDs, compared exactly.</summary>
        public static IReadOnlySet<string> Ids { get; } =
            new HashSet<string>(StringComparer.Ordinal) { "bc-bus-pass" };
    }
}
