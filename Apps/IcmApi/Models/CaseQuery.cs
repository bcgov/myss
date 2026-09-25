namespace Icm.Api.Models
{
    /// <summary>A case search: what to match, and how much to return.</summary>
    /// <remarks>
    /// <para>
    /// <b>Every criterion that is set must match</b> — they are joined with <c>AND</c> —
    /// and at least one must be set: a search of every case is refused rather than sent.
    /// Null means "not a criterion"; a blank string is refused, for the reasons
    /// <see cref="ContactQuery"/> gives.
    /// </para>
    /// <para>
    /// <b>There is no raw search expression here, deliberately</b>, and the values are
    /// vetted exactly as a contact search's are (see <see cref="ContactQuery"/>): a double
    /// quote, a wildcard or a control character is refused with an
    /// <see cref="System.ArgumentException"/> naming the property, never the value. Every
    /// criterion is matched exactly and case-sensitively, as ICM stores it.
    /// </para>
    /// <para>
    /// <b>The three key-player criteria are the contact-to-case link.</b> A case names the
    /// contact whose file it is three ways, and each finds it alone (MEASURED on SIT2 on
    /// 2026-09-24): <see cref="KeyPlayerContactId"/> is the <c>Id</c> a contact search
    /// returns, <see cref="KeyPlayerPersonId"/> its <c>Person ID ICM</c>, and
    /// <see cref="KeyPlayerIntegrationId"/> its <c>Integration Id</c>. A person who is on a
    /// case in another relationship cannot be found this way — the child collection that
    /// records that accepts no search — so "no case as key player" is not "no case".
    /// </para>
    /// </remarks>
    public class CaseQuery
    {
        /// <summary>Gets or sets the ICM row id to match (<c>Id</c>).</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the case number to match (<c>Case Num</c>), e.g. <c>1-11077140770</c>.</summary>
        public string? CaseNumber { get; set; }

        /// <summary>
        /// Gets or sets the key player's contact row id to match (<c>Key Player Id</c>) —
        /// the <see cref="Contact.Id"/> a contact search returns.
        /// </summary>
        public string? KeyPlayerContactId { get; set; }

        /// <summary>
        /// Gets or sets the key player's person number to match — the
        /// <see cref="Contact.PersonId"/> a contact search returns. ICM's field for it is
        /// misleadingly named <c>Key Player Contact Row Num</c>.
        /// </summary>
        public string? KeyPlayerPersonId { get; set; }

        /// <summary>
        /// Gets or sets the key player's integration id to match
        /// (<c>Key Player Integration Id</c>) — the <see cref="Contact.IntegrationId"/> a
        /// contact search returns.
        /// </summary>
        public string? KeyPlayerIntegrationId { get; set; }

        /// <summary>Gets or sets the status to match (<c>Status</c>), e.g. <c>Open</c>.</summary>
        public string? Status { get; set; }

        /// <summary>Gets or sets the program to match (<c>Type</c>), e.g. <c>Employment and Assistance</c>.</summary>
        public string? Type { get; set; }

        /// <summary>Gets or sets the records per page. ICM accepts 1 to 100.</summary>
        public int? PageSize { get; set; }

        /// <summary>Gets or sets the zero-based index of the first record to return.</summary>
        public int? StartRowNum { get; set; }

        /// <summary>
        /// Gets or sets the Siebel visibility mode. Null or blank means <c>Manager</c>, not
        /// ICM's default: MEASURED on SIT2 on 2026-09-24, a case is invisible under
        /// <c>Sales Rep</c> (the default), <c>Organization</c> and <c>Personal</c>, and
        /// visible under <c>Manager</c>, <c>Group</c>, <c>Sub-Organization</c> and
        /// <c>Catalog</c>. The opposite of service requests, which need <c>Organization</c>.
        /// </summary>
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether ICM should count every match as well as
        /// returning the page; the answer arrives in <see cref="CasePage.TotalCount"/>.
        /// </summary>
        public bool? IncludeTotalCount { get; set; }
    }
}
