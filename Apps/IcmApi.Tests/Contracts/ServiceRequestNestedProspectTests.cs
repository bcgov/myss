namespace Icm.Api.Tests.Contracts
{
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Icm.Api;
    using Icm.Api.Contracts;
    using Icm.Api.Models;

    /// <summary>
    /// What a service request write carries beyond its fields — the shape MEASURED on SIT2
    /// on 2026-09-29 to be the one ICM accepts: the gateway's field names, a placeholder
    /// <c>Id</c> and a <c>ListOfSRProspects</c> container on a create, and prospects nested
    /// Siebel-style inside that container.
    /// </summary>
    public class ServiceRequestNestedProspectTests
    {
        [Fact]
        public void TheProspectsTravelSiebelStyle_InsideListOfSRProspects()
        {
            SiebelServiceRequestWrite row = ServiceRequestMapper.ToSiebel(new ServiceRequestInput
            {
                Type = "Bus Pass",
                SRSubType = "Application",
                CommMethod = "Web",
                Prospects =
                [
                    new ProspectInput { FirstName = "Myss", LastName = "Direct", BirthDate = new DateOnly(2000, 1, 25), Gender = "Man/Boy" },
                    new ProspectInput { FirstName = "Myss", LastName = "Direct", BirthDate = new DateOnly(2000, 1, 25), Gender = "Man/Boy", Purpose = "Mailing" },
                ],
            });

            string json = JsonSerializer.Serialize(row, IcmRefitSettings.JsonOptions);
            using JsonDocument body = JsonDocument.Parse(json);

            JsonElement prospects = body.RootElement.GetProperty("ListOfSRProspects").GetProperty("SRProspects");
            Assert.Equal(JsonValueKind.Array, prospects.ValueKind);
            Assert.Equal(2, prospects.GetArrayLength());
            Assert.Equal("01/25/2000", prospects[0].GetProperty("Birth Date").GetString());
            Assert.Equal("Mailing", prospects[1].GetProperty("Purpose").GetString());
            Assert.False(prospects[0].TryGetProperty("Service Request Id", out _));
            Assert.False(prospects[0].TryGetProperty("Id", out _));
            Assert.False(body.RootElement.TryGetProperty("SRProspects", out _));

            // Type ahead of the sub type, whatever the alphabet says: the sub type's
            // picklist is constrained by it and Siebel validates in body order.
            string[] keys = [.. body.RootElement.EnumerateObject().Select(p => p.Name)];
            Assert.True(Array.IndexOf(keys, "Type") < Array.IndexOf(keys, "SR Sub Type"));
        }

        [Fact]
        public void ACreateCarriesAPlaceholderIdAndAnEmptyContainer_AnUpdateCarriesNeither()
        {
            SiebelServiceRequestWrite create = ServiceRequestMapper.ToSiebel(new ServiceRequestInput { Type = "Bus Pass" }, forCreate: true);
            Assert.Equal(ServiceRequestMapper.CreatePlaceholderId, create.Id);
            Assert.NotNull(create.ListOfSRProspects);
            Assert.Null(create.ListOfSRProspects.SRProspects);
            Assert.Equal(
                """{"Id":"1-MYSS","ListOfSRProspects":{},"Type":"Bus Pass"}""",
                JsonSerializer.Serialize(create, IcmRefitSettings.JsonOptions));

            SiebelServiceRequestWrite update = ServiceRequestMapper.ToSiebel(new ServiceRequestInput { Type = "Bus Pass" });
            Assert.Null(update.Id);
            Assert.Null(update.ListOfSRProspects);
            Assert.Equal("""{"Type":"Bus Pass"}""", JsonSerializer.Serialize(update, IcmRefitSettings.JsonOptions));

            // A caller's own Id is kept on a create; an empty prospect list is the same as none.
            Assert.Equal("1-X", ServiceRequestMapper.ToSiebel(new ServiceRequestInput { Id = "1-X" }, forCreate: true).Id);
            Assert.Null(ServiceRequestMapper.ToSiebel(new ServiceRequestInput { Prospects = [] }).ListOfSRProspects);
        }

        [Fact]
        public void ANestedProspectStillNeedsItsRequiredFields_AndMustNotNameAParent()
        {
            ProspectInput sound = new() { FirstName = "F", LastName = "L", BirthDate = new DateOnly(2000, 1, 25), Gender = "Man/Boy" };

            Assert.Throws<ArgumentNullException>(() => ProspectMapper.ToNestedSiebel(null!));
            Assert.Throws<ArgumentException>(() => ProspectMapper.ToNestedSiebel(new ProspectInput { LastName = "L" }));
            Assert.Throws<ArgumentException>(() => ProspectMapper.ToNestedSiebel(
                new ProspectInput { FirstName = "F", LastName = "L", BirthDate = sound.BirthDate, Gender = "Man/Boy", ServiceRequestId = "1-X" }));

            SiebelProspect row = ProspectMapper.ToNestedSiebel(sound);
            Assert.Null(row.ServiceRequestId);
            Assert.Null(row.Id);
            Assert.Equal("F", row.FirstName);

            // The same refusal reaches a service request write.
            Assert.Throws<ArgumentException>(() => ServiceRequestMapper.ToSiebel(new ServiceRequestInput
            {
                Prospects = [new ProspectInput { FirstName = "F", LastName = "L", BirthDate = sound.BirthDate, Gender = "Man/Boy", ServiceRequestId = "1-X" }],
            }));
            Assert.Throws<ArgumentNullException>(() => ServiceRequestMapper.ToSiebel((ServiceRequestInput)null!));
        }

        [Fact]
        public void TheWriteContractSpeaksTheReadContractsNames()
        {
            // Every field name on the write type is one the read contract declares — the
            // gateway's vocabulary, both ways (MEASURED 2026-09-29: "Type" is understood
            // on a write, the document's "SR Type" is ignored). The one exception is the
            // nested child container, which a read answers as a link instead.
            HashSet<string> read = [.. typeof(SiebelServiceRequest).GetProperties()
                .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
                .Where(n => n is not null)
                .Select(n => n!)];

            foreach (string name in typeof(SiebelServiceRequestWrite).GetProperties()
                .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name)
                .Where(n => n != "ListOfSRProspects"))
            {
                Assert.Contains(name, read);
            }
        }
    }
}
