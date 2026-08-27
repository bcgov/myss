namespace Icm.Api.Tests.Contracts
{
    using System.Globalization;
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
                KKCFSFlag = "N",
                ICMCGAApplicationReceivedFlag = null,
            };

            ServiceRequest model = ServiceRequestMapper.ToModel(siebel);

            Assert.True(model.RestrictedFlag);
            Assert.False(model.KKCFSFlag);
            Assert.Null(model.ICMCGAApplicationReceivedFlag);
        }

        [Fact]
        public void ToSiebel_TurnsBooleansBackIntoFlags()
        {
            ServiceRequestInput input = new() { RestrictedFlag = true, KKCFSFlag = false };

            SiebelServiceRequest siebel = ServiceRequestMapper.ToSiebel(input);

            Assert.Equal("Y", siebel.RestrictedFlag);
            Assert.Equal("N", siebel.KKCFSFlag);

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
                SRNumber = "1-12345",
                ContactCellNumber = "250-555-0100",
                Link = [new SiebelLink { Rel = "self", Href = "https://icm/sr/1", Name = "self" }],
            };

            ServiceRequest model = ServiceRequestMapper.ToModel(siebel);

            Assert.Equal("1-ABCDE", model.Id);
            Assert.Equal("1-12345", model.SRNumber);
            Assert.Equal("250-555-0100", model.ContactCellNumber);
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
                Created = "2026-08-27T10:15:00Z",                       // DTYPE_UTCDATETIME
                CallDate = "2026-08-27T14:30:00",                       // DTYPE_DATETIME
                ICMCGAResolutionDecisionDate = "2026-08-27",            // DTYPE_DATE
            };

            ServiceRequest model = ServiceRequestMapper.ToModel(siebel);

            // An instant: read as UTC, because that is what the Siebel type means.
            Assert.Equal(
                new DateTimeOffset(2026, 8, 27, 10, 15, 0, TimeSpan.Zero), model.Created);

            // No zone in the Siebel type, so none is invented here either.
            Assert.Equal(new DateTime(2026, 8, 27, 14, 30, 0), model.CallDate);
            Assert.Equal(DateTimeKind.Unspecified, model.CallDate!.Value.Kind);

            Assert.Equal(new DateOnly(2026, 8, 27), model.ICMCGAResolutionDecisionDate);
            Assert.Empty(model.UnparsedValues);
        }

        [Theory]
        [InlineData("2026-08-27T10:15:00Z")]
        [InlineData("2026-08-27T10:15:00")]
        [InlineData("2026-08-27T10:15:00+00:00")]
        [InlineData("2026-08-27T10:15")]
        [InlineData("2026-08-27 10:15:00")]
        public void ToModel_ReadsAnInstantInEveryShapeTheIsoGrammarAllows(string value)
        {
            // The grammar makes the fractional seconds and the offset optional and allows a
            // space in place of the T, so all of these are the same instant. A value with
            // no offset is UTC, because that is what the Siebel type means.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { Created = value });

            Assert.Equal(new DateTimeOffset(2026, 8, 27, 10, 15, 0, TimeSpan.Zero), model.Created);
        }

        [Theory]
        [InlineData("2026-08-27T10:15:00.123456Z")]
        [InlineData("2026-08-27T10:15:00.123456+00:00")]
        public void ToModel_KeepsFractionalSecondsOnTheWayIn(string value)
        {
            // The grammar allows six digits; nothing here rounds them off.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { Created = value });

            Assert.Equal(
                new DateTimeOffset(2026, 8, 27, 10, 15, 0, TimeSpan.Zero).AddTicks(1234560),
                model.Created);
        }

        [Theory]
        [InlineData("08/27/2026 10:15:00")]
        [InlineData("08/27/2026")]
        [InlineData("27-AUG-26 10.15.00 AM")]
        public void ToModel_RefusesToGuessAtNonIsoDates(string value)
        {
            // Siebel documents ISO 8601. A display-format value is ambiguous — 03/04/2026
            // is two different days depending on the order — so it is reported rather than
            // guessed at.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { Created = value });

            Assert.Null(model.Created);
            Assert.Equal(value, model.UnparsedValues["Created"]);
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
        public void ToModel_HonoursAnExplicitOffsetRatherThanAssumingUtc()
        {
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { Created = "2026-08-27T10:15:00-07:00" });

            Assert.Equal(new DateTimeOffset(2026, 8, 27, 17, 15, 0, TimeSpan.Zero), model.Created);
        }

        [Fact]
        public void ToModel_KeepsADateItCannotReadInsteadOfLosingIt()
        {
            // The whole point of UnparsedValues: a format we did not anticipate leaves a
            // null typed property, but the raw text survives and says so.
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { Created = "1756296900000", SRNumber = "1-1" });

            Assert.Null(model.Created);
            Assert.Equal("1756296900000", model.UnparsedValues["Created"]);

            // And the rest of the record still comes through.
            Assert.Equal("1-1", model.SRNumber);
        }

        [Fact]
        public void ToModel_TreatsEmptyAndAbsentDatesAsNullNotAsUnreadable()
        {
            ServiceRequest model = ServiceRequestMapper.ToModel(
                new SiebelServiceRequest { Created = "", Updated = "   ", CallDate = null });

            Assert.Null(model.Created);
            Assert.Null(model.Updated);
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

            Assert.Equal("2026-08-27T14:30:00", siebel.CallDate);
            Assert.Equal("2026-08-27", siebel.ICMCGAResolutionDecisionDate);
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

            Assert.Equal("2026-08-27T17:15:00Z", written);
        }

        [Fact]
        public void DateHandlingDoesNotDependOnTheMachinesCulture()
        {
            // A machine set to a day-first culture must still read and write the same way.
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("en-GB");

                ServiceRequest model = ServiceRequestMapper.ToModel(
                    new SiebelServiceRequest { Created = "2026-08-27T10:15:00Z" });

                Assert.Equal(new DateTimeOffset(2026, 8, 27, 10, 15, 0, TimeSpan.Zero), model.Created);
                Assert.Equal(
                    "2026-08-27T14:30:00",
                    SiebelDate.FromDateTime(new DateTime(2026, 8, 27, 14, 30, 0)));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
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
                new ServiceRequestQuery { Fields = ["SR Number", "Status"] });

            Assert.Equal("SR Number,Status", query.Fields);
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
