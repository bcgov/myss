namespace Myss.AdminApi.Domain
{
    /// <summary>
    /// Stable keywords for a refused confirmation message save. Apart from
    /// <see cref="Changed"/>, they match MyssWebCommon's message rules
    /// (<c>acknowledgements.ts</c>), which the admin site applies before it sends a
    /// save; if one changes there, change it here too.
    /// </summary>
    public static class SubmissionAcknowledgementKeywords
    {
        /// <summary>The page heading, alert title or message is empty or only spaces.</summary>
        public const string TextRequired = "ACKNOWLEDGEMENT.TEXT.REQUIRED";

        /// <summary>The page heading or alert title is over 100 characters, or the message over 1,000.</summary>
        public const string TextTooLong = "ACKNOWLEDGEMENT.TEXT.TOO_LONG";

        /// <summary>The message does not contain the reference number placeholder.</summary>
        public const string ReferenceMissing = "ACKNOWLEDGEMENT.BODY.REFERENCE_MISSING";

        /// <summary>The message contains a placeholder other than the reference number's.</summary>
        public const string PlaceholderUnknown = "ACKNOWLEDGEMENT.BODY.PLACEHOLDER_UNKNOWN";

        /// <summary>A link in the message does not start with https://, mailto: or tel:.</summary>
        public const string LinkInvalid = "ACKNOWLEDGEMENT.BODY.LINK_INVALID";

        /// <summary>The live message is no longer the one the edit started from.</summary>
        public const string Changed = "ACKNOWLEDGEMENT.CHANGED";
    }
}
