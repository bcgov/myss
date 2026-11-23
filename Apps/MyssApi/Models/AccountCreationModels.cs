namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Who a registration makes the account for, with that context's own
    /// answers. Only the chosen context's answers are read.
    /// </summary>
    public class AccountCreationRequestModel
    {
        /// <summary>
        /// Gets or sets the context: <c>self</c>, <c>withHelper</c>,
        /// <c>legalRepresentative</c> or <c>parentForMinor</c>. A string, so that
        /// an unknown value is refused as a field error.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// Gets or sets whether the citizen authorizes the ministry to share
        /// their information with their helper. Read for <c>withHelper</c> only.
        /// </summary>
        public bool? HelperAuthorized { get; set; }

        /// <summary>
        /// Gets or sets the person making the account. Read for
        /// <c>legalRepresentative</c> and <c>parentForMinor</c>.
        /// </summary>
        public RepresentativeRequestModel? Representative { get; set; }

        /// <summary>
        /// Gets or sets the attachment IDs of the signed consent forms. Read for
        /// <c>withHelper</c> when the helper is authorized.
        /// </summary>
        public IReadOnlyList<Guid>? ConsentDocumentIds { get; set; }

        /// <summary>
        /// Gets or sets the attachment IDs of the power of attorney documents.
        /// Read for <c>legalRepresentative</c> only.
        /// </summary>
        public IReadOnlyList<Guid>? PowerOfAttorneyDocumentIds { get; set; }
    }

    /// <summary>
    /// The person making an account for someone else.
    /// </summary>
    public class RepresentativeRequestModel
    {
        /// <summary>Gets or sets the representative's first name.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the representative's last name.</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets the representative's phone number, as typed.</summary>
        public string? Phone { get; set; }

        /// <summary>Gets or sets the agency, optional. Read for <c>legalRepresentative</c> only.</summary>
        public string? Agency { get; set; }
    }
}
