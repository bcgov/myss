namespace Icm.Api.Tests.Workflows
{
    using System.Text.Json;
    using Icm.Api;
    using Icm.Api.Workflows.Contracts;

    /// <summary>
    /// The out-args mix spaced names (<c>Error Code</c>) with unspaced ones
    /// (<c>ApplicationNumber</c>), so every property leans on its
    /// <c>[JsonPropertyName]</c> — and a typo there returns null forever rather than
    /// failing to compile.
    /// </summary>
    public class SiebelBusPassResponseSerializationTests
    {
        [Fact]
        public void Deserialize_ReadsTheSpacedAndUnspacedNamesAlike()
        {
            const string json = """
                {
                  "ApplicationNumber": "AP-12345",
                  "Error Code": "",
                  "Error Message": "",
                  "First Name": "Pat",
                  "Last Name": "Example",
                  "Status": "SUCCESS"
                }
                """;

            SiebelBusPassResponse? response = JsonSerializer.Deserialize<SiebelBusPassResponse>(
                json, IcmRefitSettings.JsonOptions);

            Assert.NotNull(response);
            Assert.Equal("AP-12345", response.ApplicationNumber);
            Assert.Equal(string.Empty, response.ErrorCode);
            Assert.Equal("Pat", response.FirstName);
            Assert.Equal("Example", response.LastName);
            Assert.Equal("SUCCESS", response.Status);
        }

        [Fact]
        public void Deserialize_KeepsFieldsTheSpecDoesNotDescribe()
        {
            // The Service Request spec was wrong about 27 of 51 names; if this one is
            // wrong too, the real names must arrive as data rather than vanish.
            const string json = """{ "Status": "SUCCESS", "Some Gateway Name": "x" }""";

            SiebelBusPassResponse? response = JsonSerializer.Deserialize<SiebelBusPassResponse>(
                json, IcmRefitSettings.JsonOptions);

            Assert.Equal("x", response!.AdditionalFields!["Some Gateway Name"].GetString());
        }
    }
}
