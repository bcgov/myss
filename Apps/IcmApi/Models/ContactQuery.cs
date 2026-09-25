namespace Icm.Api.Models
{
    using System;

    /// <summary>A contact search: what to match, and how much to return.</summary>
    /// <remarks>
    /// <para>
    /// <b>Every criterion that is set must match</b> — they are joined with <c>AND</c> —
    /// and at least one must be set: a search of everyone is refused rather than sent.
    /// Null means "not a criterion". A blank string is refused too, because
    /// <c>[SIN] = ""</c> is a real search — everyone with no SIN on file — and the usual
    /// way to get one is a missing value upstream, not an intention.
    /// </para>
    /// <para>
    /// <b>There is no raw search expression here, deliberately</b> — unlike
    /// <see cref="ServiceRequestQuery.SearchSpec"/>. The values below end up inside a Siebel
    /// expression that ICM evaluates as written, and MEASURED against SIT1 on 2026-09-17 a
    /// value carrying a double quote closes its literal and the rest is read as more
    /// expression: <c>x" OR [First Name] LIKE "*</c> returned other people's records. No
    /// escape syntax has been measured, so the library builds the expression itself and
    /// refuses any value containing a double quote, Siebel's wildcards <c>*</c> and
    /// <c>?</c>, or a control character (an <see cref="ArgumentException"/> naming the
    /// property, never the value). Pattern matching is asked for with
    /// <see cref="NameMatch"/>, not by putting wildcards in a name.
    /// </para>
    /// <para>
    /// Identifiers and phone numbers are matched exactly and case-sensitively, as ICM
    /// stores them; names and <see cref="Email"/> ignore case.
    /// </para>
    /// </remarks>
    public class ContactQuery
    {
        /// <summary>Gets or sets the ICM row id to match (<c>Id</c>).</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the person number to match (<c>Person ID ICM</c>).</summary>
        public string? PersonId { get; set; }

        /// <summary>Gets or sets the integration id to match (<c>Integration Id</c>).</summary>
        public string? IntegrationId { get; set; }

        /// <summary>
        /// Gets or sets the BC Services Card DID to match (<c>ICM BCSC DID</c>), exactly as
        /// the login issued it.
        /// </summary>
        public string? BcServicesCardDid { get; set; }

        /// <summary>
        /// Gets or sets the SIN to match. ICM holds it as nine digits with no spaces or
        /// dashes (MEASURED), and the match is exact — strip formatting first.
        /// </summary>
        public string? Sin { get; set; }

        /// <summary>Gets or sets the PHN to match. Exact; its stored format has not been seen.</summary>
        public string? Phn { get; set; }

        /// <summary>Gets or sets the first name to match, compared per <see cref="NameMatch"/>.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the middle name to match, compared per <see cref="NameMatch"/>.</summary>
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets the last name to match, compared per <see cref="NameMatch"/>.</summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Gets or sets how the three name criteria are compared. Defaults to the whole
        /// name. The partial modes need at least two characters of each name given.
        /// </summary>
        public ContactNameMatch NameMatch { get; set; } = ContactNameMatch.Exact;

        /// <summary>Gets or sets the birth date to match.</summary>
        public DateOnly? BirthDate { get; set; }

        /// <summary>Gets or sets the email to match, ignoring case (the document's <c>Email Address</c>).</summary>
        public string? Email { get; set; }

        /// <summary>Gets or sets the cell phone to match (<c>Cellular Phone #</c>), as stored.</summary>
        public string? CellPhone { get; set; }

        /// <summary>Gets or sets the home phone to match (<c>Home Phone #</c>), as stored.</summary>
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets the work phone to match (<c>Work Phone #</c>), as stored.</summary>
        public string? WorkPhone { get; set; }

        /// <summary>Gets or sets the message phone to match (<c>Message Phone</c>), as stored.</summary>
        public string? MessagePhone { get; set; }

        /// <summary>Gets or sets the records per page. ICM accepts 1 to 100.</summary>
        public int? PageSize { get; set; }

        /// <summary>Gets or sets the zero-based index of the first record to return.</summary>
        public int? StartRowNum { get; set; }

        /// <summary>
        /// Gets or sets the Siebel visibility mode. Null leaves it to ICM's default,
        /// <c>Sales Rep</c> — which, MEASURED against SIT1 on 2026-09-17, sees the same
        /// contacts <c>Organization</c> does, unlike on service requests. One trusted user
        /// on one environment; if a contact known to exist is not found, widen this first.
        /// </summary>
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ICM should count every match as well as
        /// returning the page; the answer arrives in <see cref="ContactPage.TotalCount"/>.
        /// </summary>
        public bool? IncludeTotalCount { get; set; }
    }
}
