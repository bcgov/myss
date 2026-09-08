namespace Icm.Api.Tests.Contracts
{
    using System.Text.Json;
    using Icm.Api;
    using Icm.Api.Contracts;

    /// <summary>
    /// The Siebel field names carry spaces and punctuation that no C# identifier can, so
    /// every property depends on its <c>[JsonPropertyName]</c> being right. A typo there
    /// does not fail to compile — it silently returns null forever.
    /// </summary>
    public class SiebelServiceRequestSerializationTests
    {
        [Fact]
        public void Deserialize_ReadsFieldNamesContainingSpacesAndPunctuation()
        {
            const string json = """
                {
                  "Service Request Number": "1-12345",
                  "Cell Phone": "250-555-0100",
                  "Address": "123 Main St",
                  "ICM CGA Application Received Flag": "Y",
                  "Updated Date": "2026-08-27T10:15:00Z",
                  "Id": "1-ABCDE"
                }
                """;

            SiebelServiceRequest? sr = JsonSerializer.Deserialize<SiebelServiceRequest>(
                json, IcmRefitSettings.JsonOptions);

            Assert.NotNull(sr);
            Assert.Equal("1-12345", sr.ServiceRequestNumber);
            Assert.Equal("250-555-0100", sr.CellPhone);
            Assert.Equal("123 Main St", sr.Address);
            Assert.Equal(SiebelFlag.Yes, sr.ICMCGAApplicationReceivedFlag);
            Assert.Equal("2026-08-27T10:15:00Z", sr.UpdatedDate);
            Assert.Equal("1-ABCDE", sr.Id);
        }

        [Fact]
        public void Deserialize_ReadsAListResponseAndItsLinks()
        {
            const string json = """
                {
                  "items": [ { "Service Request Number": "1-1" }, { "Service Request Number": "1-2" } ],
                  "Link": [ { "rel": "next", "href": "https://icm/next", "name": "next page" } ]
                }
                """;

            SiebelListResponse? response =
                JsonSerializer.Deserialize<SiebelListResponse>(
                    json, IcmRefitSettings.JsonOptions);

            Assert.NotNull(response);
            Assert.Equal(2, response.Items!.Count);
            Assert.Equal("1-2", response.Items[1].ServiceRequestNumber);
            Assert.Equal("next", response.Link![0].Rel);
        }

        [Fact]
        public void Deserialize_ReadsAWriteResponseWhoseItemsIsASingleObject()
        {
            const string json = """{ "items": { "Id": "1-ABCDE", "Status": "Open" } }""";

            SiebelWriteResponse? response =
                JsonSerializer.Deserialize<SiebelWriteResponse>(
                    json, IcmRefitSettings.JsonOptions);

            Assert.Equal("1-ABCDE", response!.Items!.Id);
            Assert.Equal("Open", response.Items.Status);
        }

        [Fact]
        public void Deserialize_IgnoresFieldsTheSpecDoesNotDescribe()
        {
            // Siebel is free to add fields; an unknown one must not fail the whole read.
            const string json = """{ "Service Request Number": "1-1", "Some New Siebel Field": "x" }""";

            SiebelServiceRequest? sr = JsonSerializer.Deserialize<SiebelServiceRequest>(
                json, IcmRefitSettings.JsonOptions);

            Assert.Equal("1-1", sr!.ServiceRequestNumber);
        }

        [Fact]
        public void Serialize_WritesNothingForARecordWithNoFieldsSet()
        {
            string json = JsonSerializer.Serialize(new SiebelServiceRequest(), IcmRefitSettings.JsonOptions);

            Assert.Equal("{}", json);
        }
    }
}
