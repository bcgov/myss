namespace Myss.Api.Domain
{
    /// <summary>
    /// Stable keywords accompanying every validation failure.
    /// </summary>
    /// <remarks>
    /// A keyword is the machine-readable half of an error; the message is the
    /// human half. Keeping them separate means the text can later be sourced
    /// from the content engine and translated without changing this code, and
    /// the client can key its WCAG error summary off something that does not
    /// move when wording changes. The scheme follows the handbook
    /// (<c>IDA.SIN.INVALID_CHECKSUM</c> and so on).
    /// <para>
    /// These strings are a contract shared with the browser implementation via
    /// <c>Shared/validation/validation-vectors.json</c>. Renaming one means
    /// updating that file and the TypeScript side together.
    /// </para>
    /// </remarks>
    public static class ValidationKeywords
    {
        /// <summary>An answer was supplied for a field the spec does not define.</summary>
        public const string FieldUnknown = "FORM.FIELD.UNKNOWN";

        /// <summary>A field the spec marks required has no answer.</summary>
        public const string FieldRequired = "FORM.FIELD.REQUIRED";

        /// <summary>An answer is not the JSON type the component implies.</summary>
        public const string FieldWrongType = "FORM.FIELD.WRONG_TYPE";

        /// <summary>The claimed spec version does not exist or is not published.</summary>
        public const string VersionUnknown = "FORM.VERSION.UNKNOWN";

        /// <summary>A SIN was not nine digits.</summary>
        public const string SinWrongLength = "IDA.SIN.WRONG_LENGTH";

        /// <summary>A SIN failed the Luhn mod-10 check.</summary>
        public const string SinInvalidChecksum = "IDA.SIN.INVALID_CHECKSUM";

        /// <summary>An email address is not a recognisable address.</summary>
        public const string EmailInvalidFormat = "IDA.EMAIL.INVALID_FORMAT";

        /// <summary>A confirmation field does not match the address it confirms.</summary>
        public const string EmailMismatch = "IDA.EMAIL.MISMATCH";
    }
}
