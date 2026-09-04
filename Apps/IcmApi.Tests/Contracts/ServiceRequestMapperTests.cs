namespace Icm.Api.Tests.Contracts
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;
    using Icm.Api.Contracts;
    using Icm.Api.Models;

    /// <summary>
    /// The mapping between Siebel's shape and the published models. This is where the
    /// boundary earns its keep, so it is where the boundary is checked.
    /// </summary>
    public class ServiceRequestMapperTests
    {
        [Fact]
        public void ToModel_TurnsSiebelFlagsIntoBooleans()
        {
            SiebelServiceRequest siebel = new()
            {
                RestrictedFlag = "Y",
                Kkcfs = "N",
                ICMCGAApplicationReceivedFlag = null,
            };

            ServiceRequest model = ServiceRequestMapper.ToModel(siebel);

            Assert.True(model.RestrictedFlag);
            Assert.False(model.Kkcfs);
            Assert.Null(model.ICMCGAApplicationReceivedFlag);
        }

        [Fact]
        public void ToModel_ReportsAnUnexpectedFlagValueAsUnknownNotFalse()
        {
            SiebelServiceRequest siebel = new() { RestrictedFlag = "X" };

            ServiceRequest model = ServiceRequestMapper.ToModel(siebel);

            // "X" is not an answer. On Restricted Flag in particular, false means
            // unrestricted — asserting that from a value we cannot read would be wrong.
            Assert.Null(model.RestrictedFlag);
            Assert.Equal("X", model.UnparsedValues["Restricted Flag"]);
        }

        [Fact]
        public void ToSiebel_TurnsBooleansBackIntoFlags()
        {
            ServiceRequestInput input = new() { RestrictedFlag = true, Kkcfs = false };

            SiebelServiceRequest siebel = ServiceRequestMapper.ToSiebel(input);

            Assert.Equal("Y", siebel.RestrictedFlag);
            Assert.Equal("N", siebel.Kkcfs);

            // Unset stays null so it is left out of the request entirely rather than
            // clearing the field.
            Assert.Null(siebel.ICMCGAApplicationReceivedFlag);
        }

        [Fact]
        public void ToModel_CarriesTheFieldsAndTheLinks()
        {
            SiebelServiceRequest siebel = new()
            {
                Id = "1-ABCDE",
                ServiceRequestNumber = "1-12345",
                CellPhone = "250-555-0100",
                Link = [new SiebelLink { Rel = "self", Href = "https://icm/sr/1", Name = "self" }],
            };

            ServiceRequest model = ServiceRequestMapper.ToModel(siebel);

            Assert.Equal("1-ABCDE", model.Id);
            Assert.Equal("1-12345", model.ServiceRequestNumber);
            Assert.Equal("250-555-0100", model.CellPhone);
            Assert.Equal("self", Assert.Single(model.Links).Rel);
        }

        [Fact]
        public void ToModel_GivesAnEmptyListRatherThanNullWhenThereAreNoLinks()
        {
            ServiceRequest model = ServiceRequestMapper.ToModel(new SiebelServiceRequest());

            Assert.Empty(model.Links);
        }

        [Fact]
        public void ToModel_TurnsAMissingListBodyIntoAnEmptyPage()
        {
            // What a 204 leaves behind.
            ServiceRequestPage page = ServiceRequestMapper.ToModel((SiebelListResponse?)null);

            Assert.Empty(page.Items);
            Assert.Empty(page.Links);
        }

        [Fact]
        public void ToModel_ParsesTheThreeSiebelDateTypesIntoThreeDifferentClrTypes()
        {
            SiebelServiceRequest siebel = new()
            {
                CreatedDate = "2026-08-27T10:15:00Z",                       // DTYPE_UTCDATETIME
                CallDate = "2026-08-27T14:30:00",                       // DTYPE_DATETIME
                ICMCGAResolutionDecisionDate = "2026-08-27",            // DTYPE_DATE
            };

            ServiceRequest model = ServiceRequestMapper.ToModel(siebel);

            // An instant: read as UTC, because that is what the Siebel type means.
            Assert.Equal(
                new DateTime(2026, 8, 27, 10, 15, 0), model.CreatedDate);

            // No zone in the Siebel type, so none is invented here either.
            Assert.Equal(new DateTime(2026, 8, 27, 14, 30, 0), model.CallDate);
            Assert.Equal(DateTimeKind.Unspecified, model.CallDate!.Value.Kind);

            Assert.Equal(new DateOnly(2026, 8, 27), model.ICMCGAResolutionDecisionDate);
            Assert.Empty(model.UnparsedValues);
        }

        [Theory]
        [InlineData("2026-08-27T10:15:00Z")]
        [InlineData("2026-08-27T10:15:00")]
        [InlineData("2026-08-27T10:15")]
        [InlineData("2026-08-27 10:15:00")]
        public void ToModel_ReadsAnInstantInEveryShapeTheIsoGrammarAllows(string value)
        {
            // The grammar makes the fractional seconds and the offset optional and allows a
            // space in place of the T, so all of these are the same instant. A value with
            // no offset is UTC, because that is what the Siebel type means.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CreatedDate = value });

            Assert.Equal(new DateTime(2026, 8, 27, 10, 15, 0), model.CreatedDate);
        }

        [Theory]
        [InlineData("2026-08-27T10:15:00.123456Z")]
        [InlineData("2026-08-27T10:15:00.123456+00:00")]
        public void ToUtcDateTime_KeepsFractionalSeconds(string value)
        {
            // Exercised through SiebelDate rather than through a model property: no field
            // on this business component is a DTYPE_UTCDATETIME, so there is nothing to
            // read it through — but the converter still handles the type, and another ICM
            // business component may well use it.
            Dictionary<string, string> unparsed = [];

            DateTimeOffset? parsed = SiebelDate.ToUtcDateTime(value, "Created Date", unparsed);

            Assert.Equal(
                new DateTimeOffset(2026, 8, 27, 10, 15, 0, TimeSpan.Zero).AddTicks(1234560), parsed);
            Assert.Empty(unparsed);
        }

        [Theory]
        // Real values read from SIT on 2026-08-28. The three with a second component above
        // 12 are what prove the order is month-first; the fourth is the ambiguous shape
        // that prompted the check.
        [InlineData("03/28/2016 02:55:16", 2016, 3, 28)]
        [InlineData("06/17/2026 16:17:48", 2026, 6, 17)]
        [InlineData("08/28/2026 03:23:01", 2026, 8, 28)]
        [InlineData("10/06/2015 00:20:17", 2015, 10, 6)]
        public void ToModel_ReadsTheDisplayFormatIcmActuallySends(
            string value, int year, int month, int day)
        {
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CallDate = value });

            Assert.Equal(new DateTime(year, month, day, 0, 0, 0), model.CallDate!.Value.Date);
            Assert.Empty(model.UnparsedValues);
        }

        [Theory]
        [InlineData("1756296900000")]
        [InlineData("27-AUG-26 10.15.00 AM")]
        [InlineData("not a date")]
        public void ToModel_StillReportsAShapeItDoesNotRecognise(string value)
        {
            // The safety net is not removed just because two formats are now known: a third
            // shape still arrives intact rather than as a null nobody notices.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CreatedDate = value });

            Assert.Null(model.CreatedDate);
            Assert.Equal(value, model.UnparsedValues["Created Date"]);
        }

        [Fact]
        public void ToModel_ReportsAPartialDateRatherThanCompletingIt()
        {
            // The grammar makes the month and day optional on a Date. A DateOnly cannot
            // hold that, and inventing the missing part would be worse than saying so.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { ICMCGAResolutionDecisionDate = "2026-08" });

            Assert.Null(model.ICMCGAResolutionDecisionDate);
            Assert.Equal("2026-08", model.UnparsedValues["ICM CGA Resolution Decision Date"]);
        }

        [Fact]
        public void ToModel_TakesADateAsWrittenSoAMidnightUtcValueDoesNotRollBackADay()
        {
            // The Oracle page warns that a date defaulting to midnight UTC shifts to the
            // previous day in Western Hemisphere zones — which is every zone this runs in.
            // Reading the date as written, with no zone conversion, is what avoids it.
            ServiceRequest utc = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { ICMCGAResolutionDecisionDate = "2026-09-01T00:00:00Z" });
            ServiceRequest offset = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { ICMCGAResolutionDecisionDate = "2026-09-01T00:00:00-07:00" });

            Assert.Equal(new DateOnly(2026, 9, 1), utc.ICMCGAResolutionDecisionDate);
            Assert.Equal(new DateOnly(2026, 9, 1), offset.ICMCGAResolutionDecisionDate);
        }

        [Fact]
        public void ToModel_ReportsAZonelessDateTimeAsZoneless()
        {
            // DTYPE_DATETIME has no zone, so whatever ICM attaches, the model must not
            // claim one — and the wall-clock value must survive untouched.
            ServiceRequest withZ = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CallDate = "2026-08-27T14:30:00Z" });
            ServiceRequest withOffset = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CallDate = "2026-08-27T14:30:00-07:00" });

            Assert.Equal(new DateTime(2026, 8, 27, 14, 30, 0), withZ.CallDate);
            Assert.Equal(DateTimeKind.Unspecified, withZ.CallDate!.Value.Kind);
            Assert.Equal(new DateTime(2026, 8, 27, 14, 30, 0), withOffset.CallDate);
            Assert.Equal(DateTimeKind.Unspecified, withOffset.CallDate!.Value.Kind);
        }

        [Fact]
        public void ToUtcDateTime_HonoursAnExplicitOffsetRatherThanAssumingUtc()
        {
            Dictionary<string, string> unparsed = [];

            DateTimeOffset? parsed =
                SiebelDate.ToUtcDateTime("2026-08-27T10:15:00-07:00", "Created Date", unparsed);

            Assert.Equal(new DateTimeOffset(2026, 8, 27, 17, 15, 0, TimeSpan.Zero), parsed);
        }

        [Fact]
        public void ToModel_KeepsADateItCannotReadInsteadOfLosingIt()
        {
            // The whole point of UnparsedValues: a format we did not anticipate leaves a
            // null typed property, but the raw text survives and says so.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CreatedDate = "1756296900000", ServiceRequestNumber = "1-1" });

            Assert.Null(model.CreatedDate);
            Assert.Equal("1756296900000", model.UnparsedValues["Created Date"]);

            // And the rest of the record still comes through.
            Assert.Equal("1-1", model.ServiceRequestNumber);
        }

        [Fact]
        public void ToModel_TreatsEmptyAndAbsentDatesAsNullNotAsUnreadable()
        {
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CreatedDate = "", UpdatedDate = "   ", CallDate = null });

            Assert.Null(model.CreatedDate);
            Assert.Null(model.UpdatedDate);
            Assert.Null(model.CallDate);
            Assert.Empty(model.UnparsedValues);
        }

        [Fact]
        public void ToModel_AcceptsADateFieldThatArrivesWithATimeOnIt()
        {
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { ICMCGAResolutionDecisionDate = "2026-08-27T00:00:00Z" });

            Assert.Equal(new DateOnly(2026, 8, 27), model.ICMCGAResolutionDecisionDate);
            Assert.Empty(model.UnparsedValues);
        }

        [Fact]
        public void ToSiebel_WritesDatesInSiebelsFormat()
        {
            SiebelServiceRequest siebel = ServiceRequestMapper.ToSiebel(new ServiceRequestInput
            {
                CallDate = new DateTime(2026, 8, 27, 14, 30, 0),
                ICMCGAResolutionDecisionDate = new DateOnly(2026, 8, 27),
            });

            Assert.Equal("08/27/2026 14:30:00", siebel.CallDate);
            Assert.Equal("08/27/2026", siebel.ICMCGAResolutionDecisionDate);
        }

        [Fact]
        public void ToSiebel_LeavesUnsetDatesOffTheRequest()
        {
            SiebelServiceRequest siebel = ServiceRequestMapper.ToSiebel(new ServiceRequestInput());

            Assert.Null(siebel.CallDate);
            Assert.Null(siebel.ICMCGAResolutionDecisionDate);
        }

        [Fact]
        public void ToSiebel_ConvertsToUtcBeforeWritingAnInstant()
        {
            // Sending a local time in a UTC field would be wrong by the offset with nothing
            // to signal it. Exercised through SiebelDate directly: every UTC field on the
            // record happens to be read-only, so none is reachable from ServiceRequestInput.
            string? written = SiebelDate.FromUtcDateTime(
                new DateTimeOffset(2026, 8, 27, 10, 15, 0, TimeSpan.FromHours(-7)));

            Assert.Equal("08/27/2026 17:15:00", written);
        }

        [Fact]
        public void DateHandlingDoesNotDependOnTheMachinesCulture()
        {
            // A machine set to a day-first culture must still read and write the same way.
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-GB");

                // en-GB reads 08/27 as day-first, which would throw. Invariant parsing is
                // what keeps a month-first value month-first on any machine.
                ServiceRequest model = ServiceRequestMapper.ToModel(
                    new SiebelServiceRequest { CreatedDate = "08/27/2026 10:15:00" });

                Assert.Equal(new DateTime(2026, 8, 27, 10, 15, 0), model.CreatedDate);
                Assert.Equal(
                    "08/27/2026 14:30:00",
                    SiebelDate.FromDateTime(new DateTime(2026, 8, 27, 14, 30, 0)));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [Fact]
        public void ToModel_KeepsFieldsTheClientDoesNotModel()
        {
            // The whole point: a field with no property used to vanish inside the
            // deserializer without a trace. That is how 27 of ICM's 51 fields were being
            // dropped before anyone read a raw response.
            const string json = """
                {
                  "Service Request Number": "1-11082491438",
                  "Some Field Added Upstream": "a value",
                  "A Nested One": { "x": 1 },
                  "A List": [1, 2]
                }
                """;

            SiebelServiceRequest? wire = JsonSerializer.Deserialize<SiebelServiceRequest>(
                json, IcmRefitSettings.JsonOptions);
            ServiceRequest model = ServiceRequestMapper.ToModel(wire!);

            Assert.Equal("1-11082491438", model.ServiceRequestNumber);
            Assert.Equal(3, model.AdditionalFields.Count);

            // Raw JSON, so the original type survives for review rather than being
            // flattened to a string.
            Assert.Equal("\"a value\"", model.AdditionalFields["Some Field Added Upstream"].GetRawText());
            Assert.Equal(JsonValueKind.Object, model.AdditionalFields["A Nested One"].ValueKind);
            Assert.Equal(JsonValueKind.Array, model.AdditionalFields["A List"].ValueKind);
        }

        [Fact]
        public void ToModel_KeepsTheWrittenWallClockWhenADateTimeCarriesAnOffset()
        {
            // A zone-less Siebel type reports the clock as written. DateTime parsing
            // would have converted 14:30-07:00 to the machine's local time (21:30 on a
            // UTC host) before the kind was flattened — environment-dependent corruption
            // that no test on a Pacific machine would ever see.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { CallDate = "2026-08-27T14:30:00-07:00" });

            Assert.Equal(new DateTime(2026, 8, 27, 14, 30, 0), model.CallDate);
            Assert.Equal(DateTimeKind.Unspecified, model.CallDate!.Value.Kind);
        }

        [Fact]
        public void ToSiebel_TrimsFieldNamesButKeepsTheSpacesInsideThem()
        {
            // " SR Number" would travel to ICM as a different field than "SR Number";
            // the spaces inside the name are the ones that matter.
            Icm.Api.Contracts.SiebelListQuery query = ServiceRequestMapper.ToSiebel(
                new ServiceRequestQuery { Fields = [" SR Number ", "  ", "Status"] });

            Assert.Equal("SR Number,Status", query.Fields);
        }

        [Fact]
        public void ToModel_LeavesAdditionalFieldsEmptyForAFullyModelledRecord()
        {
            SiebelServiceRequest? wire = JsonSerializer.Deserialize<SiebelServiceRequest>(
                """{ "Type": "Bus Pass", "Status": "Ready" }""", IcmRefitSettings.JsonOptions);

            ServiceRequest model = ServiceRequestMapper.ToModel(wire!);

            Assert.Equal("Bus Pass", model.Type);
            Assert.Empty(model.AdditionalFields);
        }

        [Fact]
        public void ToSiebel_NeverSendsBackFieldsItDidNotUnderstand()
        {
            // A write builds a fresh wire record, so nothing captured on a read can leak
            // into an update.
            SiebelServiceRequest written = ServiceRequestMapper.ToSiebel(
                new ServiceRequestInput { Status = "Open" });

            Assert.Null(written.AdditionalFields);
        }

        [Fact]
        public void ToModel_UsesTheFieldNamesIcmActuallySends()
        {
            // MEASURED on SIT: these four are the renames that were silently returning null
            // while the OpenAPI document's names were being used.
            const string json = """
                {
                  "Service Request Number": "1-11082491438",
                  "Type": "Bus Pass",
                  "Created Date": "08/10/2026 11:59:57",
                  "Kkcfs": "N"
                }
                """;

            SiebelServiceRequest? wire = JsonSerializer.Deserialize<SiebelServiceRequest>(
                json, IcmRefitSettings.JsonOptions);
            ServiceRequest model = ServiceRequestMapper.ToModel(wire!);

            Assert.Equal("1-11082491438", model.ServiceRequestNumber);
            Assert.Equal("Bus Pass", model.Type);
            Assert.Equal(new DateTime(2026, 8, 10, 11, 59, 57), model.CreatedDate);
            Assert.False(model.Kkcfs);
            Assert.Empty(model.AdditionalFields);
        }

        [Fact]
        public void ToSiebel_FixesUniformResponseRatherThanExposingIt()
        {
            // The spec permits only "Y", and SiebelListResponse's array shape depends on it.
            SiebelListQuery query = ServiceRequestMapper.ToSiebel(new ServiceRequestQuery());

            Assert.Equal("Y", query.UniformResponse);
        }

        [Fact]
        public void ToSiebel_JoinsRequestedFieldsIntoSiebelsCommaSeparatedList()
        {
            SiebelListQuery query = ServiceRequestMapper.ToSiebel(
                new ServiceRequestQuery { Fields = ["Service Request Number", "Status"] });

            Assert.Equal("Service Request Number,Status", query.Fields);
        }

        [Fact]
        public void ToSiebel_LeavesTheFieldListOffWhenNoneWasAskedFor()
        {
            Assert.Null(ServiceRequestMapper.ToSiebel(new ServiceRequestQuery()).Fields);
            Assert.Null(ServiceRequestMapper.ToSiebel(new ServiceRequestQuery { Fields = [] }).Fields);
        }

        [Fact]
        public void ToSiebel_MapsTheRenamedSearchOptions()
        {
            SiebelListQuery query = ServiceRequestMapper.ToSiebel(new ServiceRequestQuery
            {
                SearchSpec = "[Status] = \"Open\"",
                PageSize = 25,
                IncludeTotalCount = true,
                ExcludeEmptyFields = false,
            });

            Assert.Equal("[Status] = \"Open\"", query.SearchSpec);
            Assert.Equal(25, query.PageSize);
            Assert.True(query.RecordCountNeeded);
            Assert.False(query.ExcludeEmptyFieldsInResponse);
        }

        [Fact]
        public void ToSiebel_HandlesANullQuery()
        {
            SiebelListQuery query = ServiceRequestMapper.ToSiebel((ServiceRequestQuery?)null);

            Assert.Equal("Y", query.UniformResponse);
            Assert.Null(query.SearchSpec);
        }
    }
}
