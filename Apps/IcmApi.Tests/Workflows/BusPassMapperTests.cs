namespace Icm.Api.Tests.Workflows
{
    using System.Text.Json;
    using Icm.Api.Models;
    using Icm.Api.Workflows.Contracts;

    /// <summary>
    /// The mapper owns every bus pass translation decision — the fixed envelope, the
    /// header the retired SOAP integration sent, and which applicant fact lands in which
    /// abbreviated Siebel field. These pin each decision so a change to one is a
    /// deliberate edit here rather than a silent drift.
    /// </summary>
    public class BusPassMapperTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 3, 17, 30, 5, TimeSpan.Zero);

        private static BusPassApplication Application() => new()
        {
            RequestType = BusPassRequestType.NewApplication,
        };

        private static SiebelBusPassHeader Header(SiebelBusPassEnvelope envelope) =>
            envelope.SRInboundMessage!.ListOfICMSRBusPassInboundIO!.ICMSRInbound![0]
                .ListOfHeader!.Header![0];

        private static SiebelBusPassPayload Payload(SiebelBusPassEnvelope envelope) =>
            envelope.SRInboundMessage!.ListOfICMSRBusPassInboundIO!.ICMSRInbound![0]
                .ListOfPayload!.Payload![0];

        private static SiebelBusPassProspect Prospect(SiebelBusPassEnvelope envelope) =>
            Payload(envelope).ListOfSRProspects!.SRProspects![0];

        [Fact]
        public void TheEnvelopeIdentifiesTheIntegrationObject()
        {
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(Application(), Now);

            SiebelBusPassMessage message = envelope.SRInboundMessage!;
            Assert.Null(message.MessageId);
            Assert.Equal("Integration Object", message.MessageType);
            Assert.Equal("ICMSRBusPassInboundIO", message.IntObjectName);
            Assert.Equal("Siebel Hierarchical", message.IntObjectFormat);
        }

        [Fact]
        public void TheHeaderCarriesTheIdentitiesAndNothingElse()
        {
            // The fixed identities and the stamped timestamp are what
            // ICMClient.SetGenericHeader filled, per the INT-316 field-mapping analysis.
            // The retired integration also sent the bookkeeping slots as empty strings;
            // MEASURED SIT2 2026-09-10, submissions succeed identically without them
            // (SR 1-11085048718), so they are omitted — null here, absent on the wire.
            SiebelBusPassHeader header = Header(BusPassMapper.ToSiebel(Application(), Now));

            Assert.Equal("INT-316", header.TransactionName);
            Assert.Equal("MCP", header.SourceSystem);
            Assert.Equal("ICM", header.TargetSystem);
            Assert.Equal("MCP_proxy", header.UserId);
            Assert.Equal("SUCCESS", header.Status);
            Assert.Equal("20260903T173005Z", header.Timestamp);
            Assert.Null(header.ErrorCode);
            Assert.Null(header.ErrorMessage);
            Assert.Null(header.SourceReference);
            Assert.Null(header.TargetReference);
            Assert.Null(header.WMInstanceId);
            Assert.Null(header.Attribute1);
            Assert.Null(header.Attribute5);
        }

        [Fact]
        public void TheTimestampIsRenderedInUtcWhateverOffsetTheClockCarries()
        {
            DateTimeOffset pacific = new(2026, 9, 3, 10, 30, 5, TimeSpan.FromHours(-7));

            SiebelBusPassHeader header = Header(BusPassMapper.ToSiebel(Application(), pacific));

            Assert.Equal("20260903T173005Z", header.Timestamp);
        }

        [Theory]
        [InlineData(BusPassRequestType.NewApplication, "Application")]
        [InlineData(BusPassRequestType.AddressUpdate, "Change of Circumstance")]
        [InlineData(BusPassRequestType.Replacement, "Replacement")]
        public void TheRequestTypeIsSentOnBothThePayloadAndTheProspect(
            BusPassRequestType requestType, string expected)
        {
            // The three values MEASURED on SIT2 (2026-09-03) as the sub types the workflow
            // stores. Pinned so a correction, if the input vocabulary turns out to differ
            // from the stored one, is one deliberate edit.
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication { RequestType = requestType }, Now);

            Assert.Equal(expected, Payload(envelope).ICMBusPassRequestType);
            Assert.Equal(expected, Prospect(envelope).BusPassRequestType);

            // MEASURED SIT2 2026-09-14: the classification is the caller's, and all three
            // must travel together — the word alone stores an unclassified SR, the sub sub
            // type alone fails the upsert (bounded hierarchical picklist).
            Assert.Equal("Bus Pass", Payload(envelope).SRType);
            Assert.Equal(expected, Payload(envelope).SRSubType);
            Assert.Equal("One Address", Payload(envelope).SRSubSubType);
        }

        [Theory]
        [InlineData(BusPassRequestType.NewApplication, "AANDC Online Request")]
        [InlineData(BusPassRequestType.AddressUpdate, "Change of Circumstance")]
        [InlineData(BusPassRequestType.Replacement, "Replacement")]
        public void AFirstNationsApplicantRoutesANewApplicationAsAandc(
            BusPassRequestType requestType, string expected)
        {
            // MEASURED SIT2 2026-09-10: the old system's First Nations submission filed
            // as sub type "AANDC Online Request" (row 1-53CBZL6) — the flag exists only
            // as that distinct sub type. It qualifies a new application only; an
            // existing client's request has no applicant type on the old form.
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = requestType,
                    ApplicantType = BusPassApplicantType.FirstNations,
                },
                Now);

            Assert.Equal(expected, Payload(envelope).ICMBusPassRequestType);
            Assert.Equal(expected, Prospect(envelope).BusPassRequestType);

            // MEASURED SIT2 2026-09-14: the classification is the caller's, and all three
            // must travel together — the word alone stores an unclassified SR, the sub sub
            // type alone fails the upsert (bounded hierarchical picklist).
            Assert.Equal("Bus Pass", Payload(envelope).SRType);
            Assert.Equal(expected, Payload(envelope).SRSubType);
            Assert.Equal("One Address", Payload(envelope).SRSubSubType);
        }

        [Theory]
        [InlineData(BusPassApplicantType.Over65)]
        [InlineData(BusPassApplicantType.Neither)]
        [InlineData(null)]
        public void OtherApplicantTypesStillFileAPlainApplication(
            BusPassApplicantType? applicantType)
        {
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.NewApplication,
                    ApplicantType = applicantType,
                },
                Now);

            Assert.Equal("Application", Payload(envelope).ICMBusPassRequestType);
        }

        [Fact]
        public void TheApplicantLandsInTheProspectRow()
        {
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.NewApplication,
                    FirstName = "Pat",
                    LastName = "Example",
                    DateOfBirth = new DateOnly(1957, 3, 5),
                    SocialInsuranceNumber = "046 454 286",
                    BusPassAccountNumber = "123-456-789",
                    EmailAddress = "pat@example.com",
                    PreferredContactMethod = BusPassContactMethod.Email,
                    ResidentialAddress = new BusPassAddress
                    {
                        Unit = "4",
                        Line1 = "123 Main St",
                        Line2 = "Back door",
                        City = "Victoria",
                        Province = "BC",
                        PostalCode = "V8V 1V1",
                    },
                },
                Now);

            SiebelBusPassProspect prospect = Prospect(envelope);
            Assert.Equal("Pat", prospect.FstNme);
            Assert.Equal("Example", prospect.LstNme);
            Assert.Equal("03/05/1957", prospect.DOB);
            Assert.Equal("046454286", prospect.SIN);
            Assert.Equal("123456789", prospect.ClientId);
            Assert.Equal("pat@example.com", prospect.EmailAddress);
            Assert.Equal("Email", prospect.MethodOfCommunication);
            Assert.Equal("4", prospect.Unit);
            Assert.Equal("123 Main St", prospect.StAdd);
            Assert.Equal("Back door", prospect.StAdd2);
            Assert.Equal("Victoria", prospect.City);
            Assert.Equal("BC", prospect.Prov);
            Assert.Equal("V8V 1V1", prospect.Postal);

            // MEASURED SIT2 2026-09-14: a single-address submission stores this exact
            // combined Purpose, and FreeText is the input that produces it — Role lands
            // nowhere, so it is not sent.
            Assert.Equal("Residence/Mailing", prospect.FreeText);
            Assert.Null(prospect.Role);
        }

        [Fact]
        public void ADifferingMailingAddressBecomesASecondProspectRow()
        {
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.AddressUpdate,
                    LastName = "Example",
                    ResidentialAddress = new BusPassAddress { Line1 = "123 Main St", City = "Victoria" },
                    MailingAddress = new BusPassAddress { Line1 = "PO Box 9", City = "Vancouver" },
                },
                Now);

            Assert.Equal("Multiple Addresses", Payload(envelope).SRSubSubType);
            var prospects = Payload(envelope).ListOfSRProspects!.SRProspects!;
            Assert.Equal(2, prospects.Count);
            Assert.Equal("Residence", prospects[0].FreeText);
            Assert.Equal("123 Main St", prospects[0].StAdd);
            Assert.Equal("Mailing", prospects[1].FreeText);
            Assert.Equal("PO Box 9", prospects[1].StAdd);

            // Both rows are the same person, so the identity travels on both.
            Assert.Equal("Example", prospects[1].LstNme);
        }

        [Fact]
        public void EveryLevelCarriesAUniqueUpsertKey()
        {
            // MEASURED SIT2 2026-09-10: without a non-empty SRKey the workflow's upsert
            // fails with SBL-EAI-04397 ("No user key can be used for the Integration
            // Component instance 'Service Request'"); with these keys it creates the SR
            // (1-11085048654). The value is the submission moment, so it can never match
            // an earlier record and turn the upsert into an update.
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.AddressUpdate,
                    ResidentialAddress = new BusPassAddress { Line1 = "123 Main St" },
                    MailingAddress = new BusPassAddress { Line1 = "PO Box 9" },
                    Attachments = [new BusPassAttachment { FileName = "proof.pdf", Content = new byte[] { 1 } }],
                },
                Now);

            SiebelBusPassPayload payload = Payload(envelope);
            string srKey = payload.SRKey!;
            Assert.Matches("^MYSS-20260903173005000-[0-9a-f]{8}$", srKey);

            var prospects = payload.ListOfSRProspects!.SRProspects!;
            Assert.Equal($"{srKey}-1", prospects[0].ProspectKey);
            Assert.Equal($"{srKey}-2", prospects[1].ProspectKey);

            Assert.Equal(
                $"{srKey}-A1",
                payload.ListOfSRAttachments!.SRAttachments![0].AttKey);

            // Two submissions at the same instant still get different keys, so the
            // upsert can never match one against the other.
            SiebelBusPassEnvelope again = BusPassMapper.ToSiebel(
                new BusPassApplication { RequestType = BusPassRequestType.AddressUpdate }, Now);
            Assert.NotEqual(srKey, Payload(again).SRKey);
        }

        [Theory]
        [InlineData(BusPassPhoneType.Home, "Home Phone")]
        [InlineData(BusPassPhoneType.Work, "Work Phone")]
        [InlineData(BusPassPhoneType.Cell, "Cell Phone")]
        public void APhonePreferenceIsQualifiedByThePhoneType(
            BusPassPhoneType phoneType, string expected)
        {
            // MEASURED SIT2 2026-09-03: stored preferred-communication values are
            // "Home Phone"/"Cell Phone"/"Email", never a bare "Phone".
            SiebelBusPassProspect prospect = Prospect(BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.NewApplication,
                    PreferredContactMethod = BusPassContactMethod.Phone,
                    PhoneType = phoneType,
                },
                Now));

            Assert.Equal(expected, prospect.MethodOfCommunication);
        }

        [Fact]
        public void APhonePreferenceWithNoTypeIsOmittedRatherThanGuessed()
        {
            SiebelBusPassProspect prospect = Prospect(BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.NewApplication,
                    PreferredContactMethod = BusPassContactMethod.Phone,
                },
                Now));

            Assert.Null(prospect.MethodOfCommunication);
        }

        [Theory]
        [InlineData(BusPassPhoneType.Home)]
        [InlineData(BusPassPhoneType.Work)]
        [InlineData(BusPassPhoneType.Cell)]
        public void ThePhoneTypeChoosesTheField(BusPassPhoneType phoneType)
        {
            SiebelBusPassProspect prospect = Prospect(BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.Replacement,
                    PhoneNumber = "(250) 555-0100",
                    PhoneType = phoneType,
                },
                Now));

            string?[] typed = [prospect.HomePhone, prospect.WorkPhone, prospect.CellularPhone];
            Assert.Equal("2505550100", typed[(int)phoneType]);
            Assert.Equal(2, typed.Count(value => value is null));
            Assert.Null(prospect.Phone);

            Assert.Null(prospect.AlternatePhone);
        }

        [Theory]
        [InlineData(true, "2505550100")]
        [InlineData(false, null)]
        [InlineData(null, null)]
        public void TheLeaveMessageConsentRepeatsTheNumberInAlternatePhone(bool? allowed, string? expected)
        {
            // MEASURED SIT2 2026-09-14: the old path stores the number in Alternate Phone #
            // exactly when "leave messages" was ticked, whatever the phone type.
            SiebelBusPassProspect prospect = Prospect(BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.Replacement,
                    PhoneNumber = "(250) 555-0100",
                    PhoneType = BusPassPhoneType.Work,
                    LeaveMessageAllowed = allowed,
                },
                Now));

            Assert.Equal("2505550100", prospect.WorkPhone);
            Assert.Equal(expected, prospect.AlternatePhone);
        }

        [Fact]
        public void NoPhoneTypeFallsBackToTheUntypedFieldRatherThanGuessingOne()
        {
            SiebelBusPassProspect prospect = Prospect(BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.Replacement,
                    PhoneNumber = "250 555 0100",
                },
                Now));

            Assert.Equal("2505550100", prospect.Phone);
            Assert.Null(prospect.HomePhone);
            Assert.Null(prospect.WorkPhone);
            Assert.Null(prospect.CellularPhone);
        }

        [Fact]
        public void AttachmentsAreBase64EncodedAndAbsentWhenThereAreNone()
        {
            SiebelBusPassEnvelope without = BusPassMapper.ToSiebel(Application(), Now);
            Assert.Null(Payload(without).ListOfSRAttachments);

            BusPassApplication application = Application();
            application.Attachments = [new BusPassAttachment
            {
                FileName = "form.pdf",
                Content = new byte[] { 1, 2, 3 },
            }];
            SiebelBusPassEnvelope with = BusPassMapper.ToSiebel(application, Now);

            SiebelBusPassAttachment attachment =
                Assert.Single(Payload(with).ListOfSRAttachments!.SRAttachments!);
            Assert.Equal("form.pdf", attachment.AttName);
            Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), attachment.Base64Strng);
        }

        [Fact]
        public void TheFieldsWithNoHomeInTheIntegrationObjectAreNotSmuggledInElsewhere()
        {
            // The two acknowledgements have no field in ICMSRBusPassInboundIO, and the
            // old path leaves no trace of them either. They must not leak into Memo (which
            // a First Nations *new application* uses) or into FreeText beyond the purpose
            // words — inventing protocol would fail silently on the other end.
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication
                {
                    RequestType = BusPassRequestType.AddressUpdate,
                    ApplicantType = BusPassApplicantType.FirstNations,
                    AcknowledgedPassCancellation = true,
                    AcknowledgedEligibilityCriteria = true,
                    LeaveMessageAllowed = true,
                },
                Now);

            SiebelBusPassPayload payload = Payload(envelope);
            Assert.Null(payload.Memo);
            SiebelBusPassProspect prospect = Assert.Single(payload.ListOfSRProspects!.SRProspects!);
            Assert.Equal("Residence/Mailing", prospect.FreeText);
        }

        [Theory]
        [InlineData(BusPassRequestType.NewApplication, BusPassApplicantType.FirstNations, "New Application")]
        [InlineData(BusPassRequestType.NewApplication, BusPassApplicantType.Over65, null)]
        [InlineData(BusPassRequestType.NewApplication, null, null)]
        [InlineData(BusPassRequestType.Replacement, BusPassApplicantType.FirstNations, null)]
        public void OnlyAFirstNationsNewApplicationCarriesTheNewApplicationMemo(
            BusPassRequestType requestType, BusPassApplicantType? applicantType, string? expectedMemo)
        {
            // MEASURED SIT2 2026-09-14: the Memo is what lets an AANDC request file
            // without a contact match, exactly as the old form's First Nations submission
            // does; the old path never stores it on any other sub type.
            SiebelBusPassEnvelope envelope = BusPassMapper.ToSiebel(
                new BusPassApplication { RequestType = requestType, ApplicantType = applicantType },
                Now);

            Assert.Equal(expectedMemo, Payload(envelope).Memo);
        }

        [Fact]
        public void TheResultCarriesTheOutArgsAndAnythingUnmodelled()
        {
            BusPassResult result = BusPassMapper.ToModel(new SiebelBusPassResponse
            {
                ApplicationNumber = "AP-100",
                ErrorCode = "SBL-1",
                ErrorMessage = "boom",
                FirstName = "Pat",
                LastName = "Example",
                Status = "ERROR",
                AdditionalFields = new Dictionary<string, JsonElement>
                {
                    ["Some New Field"] = JsonDocument.Parse("\"x\"").RootElement,
                },
            });

            Assert.Equal("AP-100", result.ApplicationNumber);
            Assert.Equal("SBL-1", result.ErrorCode);
            Assert.Equal("boom", result.ErrorMessage);
            Assert.Equal("Pat", result.FirstName);
            Assert.Equal("Example", result.LastName);
            Assert.Equal("ERROR", result.Status);
            Assert.Equal("x", result.AdditionalFields["Some New Field"].GetString());
        }
    }
}
