namespace Myss.Api.Tests.Services
{
    using System.Text.Json;
    using Myss.Api.Services;

    /// <summary>
    /// Tests for <see cref="BusPassPdfDataBuilder"/>.
    /// </summary>
    public class BusPassPdfDataBuilderTests
    {
        [Fact]
        public void Build_CopiesPassthroughFieldsAsIs()
        {
            var data = Build("""{"firstName":"Ada","lastName":"Lovelace","eligibilityAcknowledged":true}""");

            Assert.Equal("Ada", data["firstName"]);
            Assert.Equal("Lovelace", data["lastName"]);
            Assert.Equal(true, data["eligibilityAcknowledged"]);
        }

        [Fact]
        public void Build_ResolvesCodedValuesToHumanLabels()
        {
            var data = Build("""{"applicantCategory":"new","phoneType":"cell"}""");

            Assert.Equal("New applicant", data["applicantCategory"]);
            Assert.Equal("Cell", data["phoneType"]);
        }

        [Fact]
        public void Build_FallsBackToTheRawCodeWhenUnmapped()
        {
            var data = Build("""{"phoneType":"pager"}""");

            Assert.Equal("pager", data["phoneType"]);
        }

        [Fact]
        public void Build_CombinesDateOfBirthWithTheMonthName()
        {
            var data = Build("""{"birthDay":"05","birthMonth":"05","birthYear":"1950"}""");

            Assert.Equal("05 May 1950", data["dateOfBirth"]);
        }

        [Fact]
        public void Build_DefaultsMailingAddressToResidentialWhenNotDifferent()
        {
            var data = Build("""
                {
                  "mailingAddressDifferent": "no",
                  "streetAddress1": "123 Main St",
                  "city": "Victoria",
                  "province": "BC",
                  "postalCode": "V8W1A1"
                }
                """);

            Assert.Equal("123 Main St", data["mailingStreetAddress1"]);
            Assert.Equal("Victoria", data["mailingCity"]);
            Assert.Equal("BC", data["mailingProvince"]);
            Assert.Equal("V8W1A1", data["mailingPostalCode"]);
        }

        [Fact]
        public void Build_KeepsTheSubmittedMailingAddressWhenDifferent()
        {
            var data = Build("""
                {
                  "mailingAddressDifferent": "yes",
                  "streetAddress1": "123 Main St",
                  "mailingStreetAddress1": "PO Box 99"
                }
                """);

            Assert.Equal("PO Box 99", data["mailingStreetAddress1"]);
        }

        private static Dictionary<string, object?> Build(string answersJson)
        {
            using JsonDocument document = JsonDocument.Parse(answersJson);
            return BusPassPdfDataBuilder.Build(document.RootElement);
        }
    }
}
