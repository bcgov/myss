namespace Myss.Api.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Declarative map from BC Bus Pass form answer keys to the PDF template's
    /// data properties.
    /// </summary>
    public static class BusPassPdfFieldMap
    {
        /// <summary>
        /// The form spec id in the content engine. Must stay in sync with
        /// BUS_PASS_FORM_SPEC_ID in MyssContent/src/lib/form-spec-seed-data.ts.
        /// </summary>
        public const string FormSpecId = "bc-bus-pass";

        /// <summary>
        /// Answer keys copied straight into the template data, keyed by the template's tag name
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> PassthroughFields = new Dictionary<string, string>
        {
            ["socialInsuranceNumber"] = "socialInsuranceNumber",
            ["busPassAccountNumber"] = "busPassAccountNumber",
            ["lastName"] = "lastName",
            ["firstName"] = "firstName",
            ["phoneNumber"] = "phoneNumber",
            ["email"] = "email",
            ["streetAddress1"] = "streetAddress1",
            ["streetAddress2"] = "streetAddress2",
            ["city"] = "city",
            ["province"] = "province",
            ["postalCode"] = "postalCode",
            ["mailingStreetAddress1"] = "mailingStreetAddress1",
            ["mailingStreetAddress2"] = "mailingStreetAddress2",
            ["mailingCity"] = "mailingCity",
            ["mailingProvince"] = "mailingProvince",
            ["mailingPostalCode"] = "mailingPostalCode",
            ["eligibilityAcknowledged"] = "eligibilityAcknowledged",
            ["leaveMessage"] = "leaveMessage",
        };

        /// <summary>
        /// Radio/select answer keys whose coded value must be resolved to its
        /// human-readable label before it reaches the PDF.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CodedValueLabels =
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["applicantCategory"] = new Dictionary<string, string>
                {
                    ["existing"] = "Existing client - address change or replacement pass",
                    ["new"] = "New applicant",
                },
                ["existingClientReason"] = new Dictionary<string, string>
                {
                    ["moved"] = "Moved - address update",
                    ["replacement"] = "Lost or stolen pass - replacement requested",
                },
                ["eligibilityCategory"] = new Dictionary<string, string>
                {
                    ["over65"] = "Over 65, within 10-year residency requirement",
                    ["firstNations"] = "First Nations reserve - band office assistance",
                    ["neither"] = "Neither of the above",
                },
                ["phoneType"] = new Dictionary<string, string>
                {
                    ["home"] = "Home",
                    ["work"] = "Work",
                    ["cell"] = "Cell",
                },
                ["preferredCommunication"] = new Dictionary<string, string>
                {
                    ["phone"] = "Phone",
                    ["email"] = "Email",
                },
                ["mailingAddressDifferent"] = new Dictionary<string, string>
                {
                    ["no"] = "No",
                    ["yes"] = "Yes",
                },
            };

        /// <summary>
        /// Form.io select codes for <c>birthMonth</c>, in the order the picklist
        /// is defined, used to render the date of birth with a month name.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> BirthMonthLabels = new Dictionary<string, string>
        {
            ["01"] = "January",
            ["02"] = "February",
            ["03"] = "March",
            ["04"] = "April",
            ["05"] = "May",
            ["06"] = "June",
            ["07"] = "July",
            ["08"] = "August",
            ["09"] = "September",
            ["10"] = "October",
            ["11"] = "November",
            ["12"] = "December",
        };
    }
}
