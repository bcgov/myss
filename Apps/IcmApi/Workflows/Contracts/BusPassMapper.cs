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

        /// <summary>The SR type every bus pass request files under.</summary>
        public const string SRTypeValue = "Bus Pass";

        /// <summary>The sub sub type of a submission with one address set.</summary>
        public const string OneAddress = "One Address";

        /// <summary>The sub sub type of a submission with a differing mailing address.</summary>
        public const string MultipleAddresses = "Multiple Addresses";

        /// <summary>The memo a First Nations new application carries (see <see cref="ToPayload"/>).</summary>
        public const string NewApplicationMemo = "New Application";

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
                    // MessageId is omitted, not sent empty. The retired SOAP integration
                    // sent the bookkeeping fields as empty strings, but MEASURED SIT2
                    // 2026-09-10: a submission without them succeeds identically
                    // (SR 1-11085048718), so only the meaningful fields are sent.
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
                                    Payload = [ToPayload(application, utcNow)],
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
                // Only the fields that carry a value. The retired integration also sent
                // WMInstanceId, the references, the error slots and Attribute1-5 as empty
                // strings; MEASURED SIT2 2026-09-10, the workflow accepts and files the
                // submission identically without them (SR 1-11085048718).
                TransactionName = TransactionName,
                UserId = UserId,
                SourceSystem = SourceSystem,
                TargetSystem = TargetSystem,
                Timestamp = utcNow.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture),
                Status = HeaderStatus,
            };

        private static SiebelBusPassPayload ToPayload(
            BusPassApplication application, DateTimeOffset utcNow)
        {
            // The integration object carries a caller-supplied key at every level
            // (SRKey / ProspectKey / AttKey) — the user keys the workflow's upsert
            // matches on. MEASURED SIT2 2026-09-10: with them absent OR empty, the
            // upsert dies at Create SR_Prospect_Att with SBL-EAI-04397 ("No user key
            // can be used for the Integration Component instance 'Service Request'").
            // The value is unique per submission so the upsert can only ever create,
            // never accidentally match and update an earlier record.
            string srKey = $"MYSS-{utcNow.UtcDateTime.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture)}";

            return new()
            {
                // The classification is the caller's, not derived. MEASURED SIT2
                // 2026-09-14: a request-type word alone stores an SR with NO sub type,
                // NO sub sub type and no prospect Purpose even when the contact matches
                // (SR 1-11086395524); a no-case contact then files as "Error - Web".
                // With SRType/SRSubType/SRSubSubType sent together, the same
                // submissions file exactly like the old path's — Replacement
                // (1-11086395541), Application for a no-case contact (1-11086395558,
                // where the old path's own filed the same way), Change of Circumstance
                // / Multiple Addresses (1-11086395579). All three are needed: SR Sub
                // Sub Type is a bounded, hierarchical picklist, and "One Address" sent
                // without its parents fails the upsert with SBL-EAI-04401 and files
                // nothing.
                ICMBusPassRequestType = ToRequestTypeValue(
                    application.RequestType, application.ApplicantType),
                SRType = SRTypeValue,
                SRSubType = ToRequestTypeValue(application.RequestType, application.ApplicantType),
                SRSubSubType = application.MailingAddress is null ? OneAddress : MultipleAddresses,

                // What makes a First Nations application file without a contact match.
                // MEASURED SIT2 2026-09-14: "AANDC Online Request" as request type and
                // sub type alone still filed an unmatched applicant as "Error - Web"
                // (1-53CJXME); with Memo "New Application" added, the same submission
                // filed exactly like the old form's — sub type "AANDC Online Request",
                // Primary Contact "No Match Row Id", that Memo (1-53CJXMS vs the old
                // path's 1-53CBZL6). The old path stores that Memo only on its First
                // Nations SR, never on Over 65 / Neither ones, so it is sent only then —
                // it also lets a plain Application through unmatched (1-53CJXN6), which
                // the old path does not do.
                Memo = application.ApplicantType == BusPassApplicantType.FirstNations
                    && application.RequestType == BusPassRequestType.NewApplication
                    ? NewApplicationMemo
                    : null,
                SRKey = srKey,
                ListOfSRProspects = new SiebelBusPassProspectList
                {
                    SRProspects = ToProspects(application, srKey),
                },
                ListOfSRAttachments = ToAttachments(application.Attachments, srKey),
            };
        }

        /// <summary>
        /// The applicant, as one prospect row per address set. MEASURED SIT2 2026-09-14:
        /// the stored row's <c>Purpose</c> is <c>Residence/Mailing</c> for a one-address
        /// submission and <c>Residence</c> for a multiple-address one (old path,
        /// 1-53CJWNO / 1-53CJUMM), and the workflow keeps only one prospect row either
        /// way — the old path's Multiple Addresses SR stores one row, and so did this
        /// client's two-row submission (1-11086395579). The second row is still sent:
        /// nothing else in the integration object carries the mailing address, and what
        /// the workflow does with it (the contact's mailing address?) is not visible
        /// from the SR.
        /// </summary>
        private static IList<SiebelBusPassProspect> ToProspects(
            BusPassApplication application, string srKey)
        {
            if (application.MailingAddress is null)
            {
                return [ToProspect(application, application.ResidentialAddress, "Residence/Mailing", $"{srKey}-1")];
            }

            return
            [
                ToProspect(application, application.ResidentialAddress, "Residence", $"{srKey}-1"),
                ToProspect(application, application.MailingAddress, "Mailing", $"{srKey}-2"),
            ];
        }

        private static SiebelBusPassProspect ToProspect(
            BusPassApplication application, BusPassAddress? address, string purpose, string prospectKey)
        {
            SiebelBusPassProspect prospect = new()
            {
                ProspectKey = prospectKey,
                FstNme = application.FirstName,
                LstNme = application.LastName,

                // MM/DD/YYYY. MEASURED SIT2 2026-09-14: "01/25/2000" sent stored as
                // Birth Date 01/25/2000 (row 1-53CJXMK) — a day above 12, so the order is
                // established, not assumed. (The retired SOAP integration sent
                // "yyyy MMM d" to the old direct-host interface.)
                DOB = SiebelDate.FromDate(application.DateOfBirth),
                SIN = Digits(application.SocialInsuranceNumber),

                // UNVERIFIED: the integration object has no field named for the bus pass
                // account number; ClientId is the only client-identifier slot, so the
                // account number rides there until the ICM team says otherwise. The
                // prospect rows read back from SIT2 show no account field at all, so
                // whatever the workflow does with this is not stored where we can see it.
                // MEASURED SIT2 2026-09-14: a prospect-level ClientId of "T9" landed in no
                // visible field either (1-11086395601); the old form's account-number
                // path has not been exercised, so this stays UNVERIFIED.
                ClientId = Digits(application.BusPassAccountNumber),
                EmailAddress = application.EmailAddress,
                MethodOfCommunication = ToContactMethodValue(
                    application.PreferredContactMethod, application.PhoneType),

                // UNVERIFIED: the prospect row repeats the request type the payload
                // carries; both fields exist and nothing says which one the workflow
                // reads, so both are sent with the same value.
                BusPassRequestType = ToRequestTypeValue(
                    application.RequestType, application.ApplicantType),

                // FreeText is what the stored row's Purpose comes from. MEASURED SIT2
                // 2026-09-14 by elimination: Role alone — as "Residence/Mailing"
                // (1-11086395541) or "Residential" (1-11086395619) — stores no Purpose
                // and lands in no visible field at all; the one submission that carried
                // FreeText = "Residence/Mailing" (1-11086395601) stored exactly that as
                // Purpose. Role is therefore not sent.
                FreeText = purpose,
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

            // Alternate Phone # is how the old path carries the leave-a-message consent.
            // MEASURED SIT2 2026-09-14 on four old-form submissions made for this: the one
            // with "leave messages" ticked stores the number in Alternate Phone # too
            // (1-53CJUMM); the three without do not (1-53CJUNB, 1-53CJXFC, 1-53CJV62),
            // whatever the phone type. AlternatePhone# on the wire fills it
            // (1-11086395601).
            if (application.LeaveMessageAllowed == true)
            {
                prospect.AlternatePhone = phone;
            }

            return prospect;
        }

        private static SiebelBusPassAttachmentList? ToAttachments(
            IReadOnlyList<BusPassAttachment>? attachments, string srKey)
        {
            if (attachments is null || attachments.Count == 0)
            {
                return null;
            }

            return new SiebelBusPassAttachmentList
            {
                SRAttachments = [.. attachments.Select((attachment, index) => new SiebelBusPassAttachment
                {
                    AttKey = $"{srKey}-A{index + 1}",
                    AttName = attachment.FileName,
                    Base64Strng = Convert.ToBase64String(attachment.Content.Span),
                })],
            };
        }

        /// <summary>
        /// The request-type value, sent both as <c>ICMBusPassRequestType</c> and as the
        /// SR sub type. MEASURED SIT2 2026-09-03: SRs the workflow created carry exactly
        /// these three as their sub type (<c>Application</c>, <c>Change of
        /// Circumstance</c>, <c>Replacement</c>); MEASURED 2026-09-14 that sending the
        /// same words as <c>SRSubType</c> files them that way, and that the old form's
        /// Over 65 and Neither applicant types both file as plain <c>Application</c>
        /// (1-53CJUNB, 1-53CJXFC) — nothing distinguishes them on the stored SR.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A First Nations new application is not an <c>Application</c>: MEASURED SIT2
        /// 2026-09-10, the old system's submission with the First Nations flag on filed
        /// as sub type <c>AANDC Online Request</c> (row 1-53CBZL6) — the flag exists
        /// only as that distinct sub type, and that path files cleanly even without a
        /// contact match (the band office follows up instead). The applicant type only
        /// qualifies a new application; an existing client's request has no applicant
        /// type on the old form, so it never rewrites the other two words.
        /// </para>
        /// <para>
        /// <b>Sending the word is not enough.</b> MEASURED 2026-09-10 (row 1-53BR2CI)
        /// and again 2026-09-14 with the full classification triple (row 1-53CJXME):
        /// <c>AANDC Online Request</c> as request type and sub type is accepted, but an
        /// unmatched applicant still goes down the match-required path and files as
        /// <c>Error - Web</c> with the sub type overwritten, where the old path's First
        /// Nations flag accepted the same unmatched applicant (1-53CBZL6). Whatever
        /// grants the AANDC no-match acceptance, this integration object does not
        /// expose it — the word stays as the best-guess vocabulary, and the real
        /// trigger is an open question for the ICM team.
        /// </para>
        /// </remarks>
        private static string ToRequestTypeValue(
            BusPassRequestType requestType, BusPassApplicantType? applicantType) =>
            requestType switch
            {
                BusPassRequestType.NewApplication when applicantType == BusPassApplicantType.FirstNations
                    => "AANDC Online Request",
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
