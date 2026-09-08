namespace Icm.Api.Workflows.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using Icm.Api.Contracts;
    using Icm.Api.Models;

    /// <summary>
    /// Converts between the published bus pass models and the workflow's wire envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one place the two shapes meet, like <see cref="Icm.Api.Contracts.ServiceRequestMapper"/>
    /// for service requests. Everything below speaks the integration object's terms;
    /// everything above speaks <see cref="BusPassApplication"/>.
    /// </para>
    /// <para>
    /// <b>The vocabulary here is read off real workflow output.</b> MEASURED against SIT2
    /// on 2026-09-03: SRs this workflow created (2022 through 2026-08, <c>Created By
    /// SIEBEL_EAI</c>, <c>Comm Method Web</c>) carry <c>SR Type "Bus Pass"</c>, sub type
    /// <c>Application</c>/<c>Change of Circumstance</c>/<c>Replacement</c>, sub sub type
    /// <c>One Address</c>/<c>Multiple Addresses</c>; their <c>SRProspects</c> rows carry
    /// <c>Preferred Communication Method</c> values <c>Home Phone</c>/<c>Cell Phone</c>/
    /// <c>Email</c> and <c>Purpose</c> values <c>Residence</c>/<c>Residence/Mailing</c>,
    /// with SIN and phones as bare digits. What remains inference is which <i>input</i>
    /// field produces each stored value — marked UNVERIFIED at the line that assumes it
    /// and collected in the README.
    /// </para>
    /// </remarks>
    internal static class BusPassMapper
    {
        /// <summary>The <c>MessageType</c> every message carries.</summary>
        public const string MessageType = "Integration Object";

        /// <summary>The integration object this envelope serializes.</summary>
        public const string IntObjectName = "ICMSRBusPassInboundIO";

        /// <summary>The <c>IntObjectFormat</c> every message carries.</summary>
        public const string IntObjectFormat = "Siebel Hierarchical";

        /// <summary>
        /// The name of MySS's integration, not of the workflow: INT-316 identifies the
        /// caller's bus pass transaction to ICM (per the field-mapping analysis of the
        /// retired integration), and is the only MySS integration that calls the
        /// <c>ICM Receive Bus Pass Online Request Wrapper WF</c> workflow — which is
        /// ICM's, and serves other callers too.
        /// </summary>
        public const string TransactionName = "INT-316";

        /// <summary>
        /// The source system named in the header. The retired integration identified
        /// itself as <c>MCP</c>; kept for continuity, since ICM-side routing may key on
        /// it. UNVERIFIED whether ICM wants a new name for MySS.
        /// </summary>
        public const string SourceSystem = "MCP";

        /// <summary>The target system named in the header.</summary>
        public const string TargetSystem = "ICM";

        /// <summary>
        /// The user id named in the header. The retired integration sent
        /// <c>MCP_proxy</c>; kept for the same continuity reason as
        /// <see cref="SourceSystem"/>, and UNVERIFIED for the same reason.
        /// </summary>
        public const string UserId = "MCP_proxy";

        /// <summary>The header status an outbound message carries.</summary>
        public const string HeaderStatus = "SUCCESS";

        /// <summary>
        /// The header timestamp format the retired integration used
        /// (<c>DateTime.UtcNow</c> as <c>yyyyMMddTHHmmssZ</c>).
        /// </summary>
        private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

        /// <summary>Converts a submission to the wire envelope.</summary>
        /// <param name="application">The request as the applicant stated it.</param>
        /// <param name="utcNow">The moment stamped into the header, in UTC.</param>
        /// <returns>The envelope, ready to POST.</returns>
        public static SiebelBusPassEnvelope ToSiebel(
            BusPassApplication application,
            DateTimeOffset utcNow)
        {
            ArgumentNullException.ThrowIfNull(application);

            return new SiebelBusPassEnvelope
            {
                SRInboundMessage = new SiebelBusPassMessage
                {
                    // Empty rather than omitted: the retired integration sent the
                    // identification and bookkeeping fields as empty strings, and the
                    // receiving side is old enough to care about the difference.
                    MessageId = string.Empty,
                    MessageType = MessageType,
                    IntObjectName = IntObjectName,
                    IntObjectFormat = IntObjectFormat,
                    ListOfICMSRBusPassInboundIO = new SiebelBusPassInboundList
                    {
                        ICMSRInbound =
                        [
                            new SiebelBusPassInbound
                            {
                                ListOfHeader = new SiebelBusPassHeaderList
                                {
                                    Header = [ToHeader(utcNow)],
                                },
                                ListOfPayload = new SiebelBusPassPayloadList
                                {
                                    Payload = [ToPayload(application)],
                                },
                            },
                        ],
                    },
                },
            };
        }

        /// <summary>Converts the workflow's out-args to the published result.</summary>
        /// <param name="response">The body the workflow returned.</param>
        /// <returns>The published result.</returns>
        public static BusPassResult ToModel(SiebelBusPassResponse response) =>
            new()
            {
                ApplicationNumber = response.ApplicationNumber,
                ErrorCode = response.ErrorCode,
                ErrorMessage = response.ErrorMessage,
                FirstName = response.FirstName,
                LastName = response.LastName,
                Status = response.Status,
                AdditionalFields = response.AdditionalFields is null
                    ? new Dictionary<string, JsonElement>()
                    : new Dictionary<string, JsonElement>(response.AdditionalFields),
            };

        private static SiebelBusPassHeader ToHeader(DateTimeOffset utcNow) =>
            new()
            {
                TransactionName = TransactionName,
                WMInstanceId = string.Empty,
                SourceReference = string.Empty,
                TargetReference = string.Empty,
                UserId = UserId,
                SourceSystem = SourceSystem,
                TargetSystem = TargetSystem,
                Timestamp = utcNow.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                Status = HeaderStatus,
                ErrorCode = string.Empty,
                ErrorMessage = string.Empty,
                Attribute1 = string.Empty,
                Attribute2 = string.Empty,
                Attribute3 = string.Empty,
                Attribute4 = string.Empty,
                Attribute5 = string.Empty,
            };

        private static SiebelBusPassPayload ToPayload(BusPassApplication application) =>
            new()
            {
                // The SR classification fields (SRType, SRSubType, Status, …) stay unset:
                // MEASURED SIT2 2026-09-03, the workflow derives them itself — SR Type
                // "Bus Pass", the sub type from the request type, the sub sub type
                // "One Address"/"Multiple Addresses" from how many address sets arrive.
                ICMBusPassRequestType = ToRequestTypeValue(application.RequestType),
                ListOfSRProspects = new SiebelBusPassProspectList
                {
                    SRProspects = ToProspects(application),
                },
                ListOfSRAttachments = ToAttachments(application.Attachments),
            };

        /// <summary>
        /// The applicant, as one prospect row per address set. MEASURED SIT2 2026-09-03:
        /// a single-address submission is stored as one row whose <c>Purpose</c> is
        /// <c>Residence/Mailing</c>, and the SR sub sub type says <c>One Address</c> or
        /// <c>Multiple Addresses</c>. UNVERIFIED: that a differing mailing address goes in
        /// as a second row with role <c>Mailing</c> is the natural reading of those two
        /// facts, but no stored <c>Mailing</c> row has been observed — the ones sampled
        /// may simply not have kept it.
        /// </summary>
        private static IList<SiebelBusPassProspect> ToProspects(BusPassApplication application)
        {
            if (application.MailingAddress is null)
            {
                return [ToProspect(application, application.ResidentialAddress, "Residence/Mailing")];
            }

            return
            [
                ToProspect(application, application.ResidentialAddress, "Residence"),
                ToProspect(application, application.MailingAddress, "Mailing"),
            ];
        }

        private static SiebelBusPassProspect ToProspect(
            BusPassApplication application, BusPassAddress? address, string role)
        {
            SiebelBusPassProspect prospect = new()
            {
                FstNme = application.FirstName,
                LstNme = application.LastName,

                // MM/DD/YYYY, the one date shape this gateway has ever been seen to use
                // (stored Birth Dates read back that way). The retired SOAP integration
                // sent "yyyy MMM d" to the old direct-host interface; UNVERIFIED which of
                // the two this workflow parses, and a wrong one fails to match a client
                // rather than failing the call.
                DOB = SiebelDate.FromDate(application.DateOfBirth),
                SIN = Digits(application.SocialInsuranceNumber),

                // UNVERIFIED: the integration object has no field named for the bus pass
                // account number; ClientId is the only client-identifier slot, so the
                // account number rides there until the ICM team says otherwise. The
                // prospect rows read back from SIT2 show no account field at all, so
                // whatever the workflow does with this is not stored where we can see it.
                ClientId = Digits(application.BusPassAccountNumber),
                EmailAddress = application.EmailAddress,
                MethodOfCommunication = ToContactMethodValue(
                    application.PreferredContactMethod, application.PhoneType),

                // UNVERIFIED: the prospect row repeats the request type the payload
                // carries; both fields exist and nothing says which one the workflow
                // reads, so both are sent with the same value.
                BusPassRequestType = ToRequestTypeValue(application.RequestType),

                // UNVERIFIED name-to-name, but the stored rows carry a Purpose of exactly
                // these values, and Role is the only prospect input field left to feed it.
                Role = role,
                Unit = address?.Unit,
                StAdd = address?.Line1,
                StAdd2 = address?.Line2,
                City = address?.City,
                Prov = address?.Province,
                Postal = address?.PostalCode,
            };

            // The old form sent one number plus a type code; this object has a field per
            // type instead, so the type picks the field. No type falls back to the
            // untyped Phone field rather than guessing one.
            string? phone = Digits(application.PhoneNumber);
            switch (application.PhoneType)
            {
                case BusPassPhoneType.Home:
                    prospect.HomePhone = phone;
                    break;
                case BusPassPhoneType.Work:
                    prospect.WorkPhone = phone;
                    break;
                case BusPassPhoneType.Cell:
                    prospect.CellularPhone = phone;
                    break;
                default:
                    prospect.Phone = phone;
                    break;
            }

            return prospect;
        }

        private static SiebelBusPassAttachmentList? ToAttachments(
            IReadOnlyList<BusPassAttachment>? attachments)
        {
            if (attachments is null || attachments.Count == 0)
            {
                return null;
            }

            return new SiebelBusPassAttachmentList
            {
                SRAttachments = [.. attachments.Select(attachment => new SiebelBusPassAttachment
                {
                    AttName = attachment.FileName,
                    Base64Strng = Convert.ToBase64String(attachment.Content.Span),
                })],
            };
        }

        /// <summary>
        /// The request-type value. MEASURED SIT2 2026-09-03: SRs the workflow created
        /// carry exactly these three as their sub type (<c>Application</c>,
        /// <c>Change of Circumstance</c>, <c>Replacement</c>). UNVERIFIED only in that the
        /// input is assumed to use the same words the output stores; a wrong value is not
        /// rejected — the field is free text on the wire — it misroutes the request.
        /// </summary>
        private static string ToRequestTypeValue(BusPassRequestType requestType) =>
            requestType switch
            {
                BusPassRequestType.NewApplication => "Application",
                BusPassRequestType.AddressUpdate => "Change of Circumstance",
                BusPassRequestType.Replacement => "Replacement",
                _ => throw new ArgumentOutOfRangeException(nameof(requestType), requestType, null),
            };

        /// <summary>
        /// The contact-method value. MEASURED SIT2 2026-09-03: stored prospect rows carry
        /// <c>Preferred Communication Method</c> values of <c>Home Phone</c>,
        /// <c>Cell Phone</c> and <c>Email</c> — the phone preference is qualified by which
        /// phone, not the bare "Phone" the old form sent. So a phone preference borrows
        /// the phone type; one with no type is omitted rather than guessed.
        /// </summary>
        private static string? ToContactMethodValue(
            BusPassContactMethod? method, BusPassPhoneType? phoneType) =>
            method switch
            {
                BusPassContactMethod.Email => "Email",
                BusPassContactMethod.Phone => phoneType switch
                {
                    BusPassPhoneType.Home => "Home Phone",
                    BusPassPhoneType.Work => "Work Phone",
                    BusPassPhoneType.Cell => "Cell Phone",
                    null => null,
                    _ => throw new ArgumentOutOfRangeException(nameof(phoneType), phoneType, null),
                },
                null => null,
                _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
            };

        /// <summary>
        /// Strips a value to its digits, as the old integration did for SIN and phone —
        /// the form captured them masked (<c>999 999 999</c>, <c>(999) 999-9999</c>).
        /// Null in, null out; a value with no digits at all is also null.
        /// </summary>
        private static string? Digits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string digits = new([.. value.Where(char.IsAsciiDigit)]);
            return digits.Length == 0 ? null : digits;
        }
    }
}
