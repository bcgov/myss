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
    /// move when wording changes. The scheme is a dotted, domain-scoped
    /// <c>DOMAIN.CONTEXT.NAME</c> (<c>IDA.SIN.INVALID_CHECKSUM</c> and so on),
    /// and a keyword is never reused for a different meaning.
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

        /// <summary>An answer does not match the field's <c>validate.pattern</c>.</summary>
        public const string FieldPattern = "FORM.FIELD.PATTERN";

        /// <summary>An answer is shorter than the field's <c>validate.minLength</c>.</summary>
        public const string FieldMinLength = "FORM.FIELD.MIN_LENGTH";

        /// <summary>An answer is longer than the field's <c>validate.maxLength</c>.</summary>
        public const string FieldMaxLength = "FORM.FIELD.MAX_LENGTH";

        /// <summary>A number is under the field's <c>validate.min</c>.</summary>
        public const string FieldMin = "FORM.FIELD.MIN";

        /// <summary>A number is over the field's <c>validate.max</c>.</summary>
        public const string FieldMax = "FORM.FIELD.MAX";

        /// <summary>A date field, or a day/month/year group, is not a real date.</summary>
        public const string DateInvalid = "FORM.DATE.INVALID";

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

        /// <summary>A phone number is not ten digits once formatting is stripped.</summary>
        public const string PhoneInvalidFormat = "IDA.PHONE.INVALID_FORMAT";

        /// <summary>A postal code is not in the Canadian A1A 1A1 format.</summary>
        public const string PostalCodeInvalidFormat = "IDA.POSTAL_CODE.INVALID_FORMAT";

        /// <summary>A registration date of birth is not a valid date.</summary>
        public const string RegistrationDateOfBirthInvalid = "REGISTRATION.DOB.INVALID";

        /// <summary>A registration date of birth must not be in the future.</summary>
        public const string RegistrationDateOfBirthInFuture = "REGISTRATION.DOB.FUTURE";

        /// <summary>A registration phone number is not a ten-digit North American number.</summary>
        public const string RegistrationPhoneInvalid = "REGISTRATION.PHONE.INVALID";

        /// <summary>A registration gender is not one of the form's gender options.</summary>
        public const string RegistrationGenderUnknown = "REGISTRATION.GENDER.UNKNOWN";
    }
}
