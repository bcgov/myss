namespace Icm.Api.ConsoleApp.Configuration
{
    using Icm.Api.Models;

    /// <summary>
    /// The bus pass submission to send when <c>Mode</c> is <c>buspass</c>. Mirrors
    /// <see cref="BusPassApplication"/> in configuration-friendly strings.
    /// </summary>
    /// <remarks>
    /// The committed defaults are synthetic on purpose — the SIN is the canonical test
    /// number, the address is the legislature's. A run against SIT creates a real service
    /// request over there, so the data should be obviously fake to whoever finds it.
    /// </remarks>
    public class BusPassSettings
    {
        /// <summary>
        /// Gets or sets the request type: <c>NewApplication</c>, <c>AddressUpdate</c> or
        /// <c>Replacement</c>.
        /// </summary>
        public string RequestType { get; set; } = "NewApplication";

        /// <summary>Gets or sets the applicant's first name.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the applicant's last name.</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets the SIN.</summary>
        public string? Sin { get; set; }

        /// <summary>Gets or sets the bus pass account number.</summary>
        public string? BusPassAccountNumber { get; set; }

        /// <summary>Gets or sets the date of birth, e.g. <c>1957-03-05</c>.</summary>
        public DateOnly? DateOfBirth { get; set; }

        /// <summary>Gets or sets the phone number.</summary>
        public string? PhoneNumber { get; set; }

        /// <summary>Gets or sets the phone type: <c>Home</c>, <c>Work</c> or <c>Cell</c>.</summary>
        public string? PhoneType { get; set; }

        /// <summary>Gets or sets the email address.</summary>
        public string? EmailAddress { get; set; }

        /// <summary>Gets or sets the preferred contact method: <c>Phone</c> or <c>Email</c>.</summary>
        public string? PreferredContactMethod { get; set; }

        /// <summary>Gets or sets the residential address.</summary>
        public AddressSettings Residential { get; set; } = new();

        /// <summary>
        /// Gets or sets the mailing address. Leave every field empty when it is the same
        /// as the residential one.
        /// </summary>
        public AddressSettings Mailing { get; set; } = new();

        /// <summary>
        /// Checks the settings parse, so a typo'd enum fails before a token is fetched.
        /// </summary>
        /// <param name="problems">Collects what is wrong.</param>
        public void Validate(List<string> problems)
        {
            if (!Enum.TryParse<BusPassRequestType>(RequestType, ignoreCase: true, out _))
            {
                problems.Add(
                    $"BusPass:RequestType '{RequestType}' is not one of "
                    + "NewApplication, AddressUpdate, Replacement.");
            }

            if (!string.IsNullOrWhiteSpace(PhoneType)
                && !Enum.TryParse<BusPassPhoneType>(PhoneType, ignoreCase: true, out _))
            {
                problems.Add($"BusPass:PhoneType '{PhoneType}' is not one of Home, Work, Cell.");
            }

            if (!string.IsNullOrWhiteSpace(PreferredContactMethod)
                && !Enum.TryParse<BusPassContactMethod>(PreferredContactMethod, ignoreCase: true, out _))
            {
                problems.Add(
                    $"BusPass:PreferredContactMethod '{PreferredContactMethod}' is not one of Phone, Email.");
            }
        }

        /// <summary>Converts these settings into the application the library takes.</summary>
        /// <returns>The application.</returns>
        public BusPassApplication ToApplication() =>
            new()
            {
                RequestType = Enum.Parse<BusPassRequestType>(RequestType, ignoreCase: true),
                FirstName = NullIfBlank(FirstName),
                LastName = NullIfBlank(LastName),
                SocialInsuranceNumber = NullIfBlank(Sin),
                BusPassAccountNumber = NullIfBlank(BusPassAccountNumber),
                DateOfBirth = DateOfBirth,
                PhoneNumber = NullIfBlank(PhoneNumber),
                PhoneType = string.IsNullOrWhiteSpace(PhoneType)
                    ? null
                    : Enum.Parse<BusPassPhoneType>(PhoneType, ignoreCase: true),
                EmailAddress = NullIfBlank(EmailAddress),
                PreferredContactMethod = string.IsNullOrWhiteSpace(PreferredContactMethod)
                    ? null
                    : Enum.Parse<BusPassContactMethod>(PreferredContactMethod, ignoreCase: true),
                ResidentialAddress = Residential.ToAddress(),
                MailingAddress = Mailing.ToAddress(),
            };

        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>One address set.</summary>
    public class AddressSettings
    {
        /// <summary>Gets or sets the unit or suite number.</summary>
        public string? Unit { get; set; }

        /// <summary>Gets or sets the first street address line.</summary>
        public string? Line1 { get; set; }

        /// <summary>Gets or sets the second street address line.</summary>
        public string? Line2 { get; set; }

        /// <summary>Gets or sets the city.</summary>
        public string? City { get; set; }

        /// <summary>Gets or sets the province.</summary>
        public string? Province { get; set; }

        /// <summary>Gets or sets the postal code.</summary>
        public string? PostalCode { get; set; }

        /// <summary>Converts to the library's address, or null when every field is empty.</summary>
        /// <returns>The address, or null.</returns>
        public BusPassAddress? ToAddress()
        {
            if (string.IsNullOrWhiteSpace(Unit) && string.IsNullOrWhiteSpace(Line1)
                && string.IsNullOrWhiteSpace(Line2) && string.IsNullOrWhiteSpace(City)
                && string.IsNullOrWhiteSpace(Province) && string.IsNullOrWhiteSpace(PostalCode))
            {
                return null;
            }

            return new BusPassAddress
            {
                Unit = Blank(Unit),
                Line1 = Blank(Line1),
                Line2 = Blank(Line2),
                City = Blank(City),
                Province = Blank(Province),
                PostalCode = Blank(PostalCode),
            };
        }

        private static string? Blank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
