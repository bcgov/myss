namespace Myss.Api.Data
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The compiled wording for every stable error keyword: what MyssApi serves
    /// when the content engine cannot be read, and the text a catalogue row
    /// starts from on a fresh database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The live catalogue is the <c>error-message</c> collection in Strapi,
    /// read through <see cref="Myss.Api.Providers.StrapiErrorMessageProvider"/>
    /// and overlaid on this table keyword by keyword. A keyword the catalogue
    /// has no row for keeps the wording here, so nothing ever goes unworded.
    /// </para>
    /// <para>
    /// These values MUST stay identical to <c>Shared/validation/error-messages.json</c>,
    /// the contract shared with the Strapi seed
    /// (<c>Apps/MyssContent/src/lib/error-message-seed-data.ts</c>). The test
    /// suite links that file and fails on any divergence. The rules for what
    /// belongs in the catalogue, and why <c>FORM.FIELD.REQUIRED</c> is absent,
    /// are in the shared file's header.
    /// </para>
    /// </remarks>
    public static class ErrorMessageDefaults
    {
        /// <summary>Gets the compiled wording, keyed by keyword.</summary>
        public static IReadOnlyDictionary<string, string> Messages { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["IDA.SIN.WRONG_LENGTH"] = "A Social Insurance Number must be 9 digits.",
                ["IDA.SIN.INVALID_CHECKSUM"] = "That Social Insurance Number is not valid. Check the digits and try again.",
                ["IDA.EMAIL.INVALID_FORMAT"] = "Enter an email address in the format name@example.com.",
                ["IDA.EMAIL.MISMATCH"] = "The two email addresses do not match.",
                ["IDA.PHONE.INVALID_FORMAT"] = "Enter a 10-digit phone number, for example 250 555 0199.",
                ["IDA.POSTAL_CODE.INVALID_FORMAT"] = "Enter a postal code in the format A1A 1A1.",
                ["FORM.DATE.INVALID"] = "Enter a valid date.",
                ["FORM.FIELD.PATTERN"] = "This answer is not in the expected format.",
                ["FORM.FIELD.MIN_LENGTH"] = "This answer is too short.",
                ["FORM.FIELD.MAX_LENGTH"] = "This answer is too long.",
                ["FORM.FIELD.MIN"] = "This number is too small.",
                ["FORM.FIELD.MAX"] = "This number is too large.",
                ["REGISTRATION.DOB.INVALID"] = "Enter a valid date of birth.",
                ["REGISTRATION.DOB.FUTURE"] = "Date of birth cannot be in the future.",
                ["BUSPASS.REQUEST.TYPE_INVALID"] = "Select a valid service type",
                ["BUSPASS.IDENTITY.IDENTIFIER_REQUIRED"] = "A Social Insurance Number or bus pass account number is required",
                ["BUSPASS.DOB.INVALID"] = "Enter a valid date of birth",
                ["BUSPASS.DOB.FUTURE"] = "The date of birth cannot be in the future",
                ["BUSPASS.DOB.UNDER_16"] = "You must be at least 16",
                ["BUSPASS.CONTACT.EMAIL_REQUIRED"] = "An email address is required when email is the preferred means of communication",
                ["BUSPASS.CONTACT.EMAIL_MISMATCH"] = "The two email addresses do not match",
                ["BUSPASS.SUBMIT.REJECTED"] = "The BC Bus Pass Program could not accept this request. Check that the details you entered match what the ministry has on file, or contact the program for help.",
                ["BUSPASS.SUBMIT.RATE_LIMITED"] = "Too many requests have been sent from your connection in a short time. Wait a few minutes and try again.",
            }.AsReadOnly();
    }
}
