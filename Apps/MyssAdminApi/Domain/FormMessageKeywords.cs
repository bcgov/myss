namespace Myss.AdminApi.Domain
{
    /// <summary>
    /// Stable keywords for a refused form message save. Apart from
    /// <see cref="Changed"/>, they match MyssWebCommon's message rules
    /// (<c>formMessages.ts</c>), which the admin site applies before it sends a
    /// save; if one changes there, change it here too.
    /// </summary>
    public static class FormMessageKeywords
    {
        /// <summary>The page heading, title or message is empty or only spaces.</summary>
        public const string TextRequired = "FORM_MESSAGE.TEXT.REQUIRED";

        /// <summary>The page heading or title is over 100 characters, or the message over 1,000.</summary>
        public const string TextTooLong = "FORM_MESSAGE.TEXT.TOO_LONG";

        /// <summary>The message lacks a placeholder its definition requires.</summary>
        public const string PlaceholderMissing = "FORM_MESSAGE.BODY.PLACEHOLDER_MISSING";

        /// <summary>The message contains a placeholder its definition does not list.</summary>
        public const string PlaceholderUnknown = "FORM_MESSAGE.BODY.PLACEHOLDER_UNKNOWN";

        /// <summary>A link in the message does not start with https://, mailto: or tel:.</summary>
        public const string LinkInvalid = "FORM_MESSAGE.BODY.LINK_INVALID";

        /// <summary>The live message is no longer the one the edit started from.</summary>
        public const string Changed = "FORM_MESSAGE.CHANGED";
    }
}
