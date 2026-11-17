namespace Icm.Api.Models
{
    /// <summary>A contact to link to a service request: who, and in what relationship.</summary>
    /// <remarks>
    /// <para>
    /// The write ICM's REST API guidance ("PUT Contact to Service Request", ADO 8628)
    /// describes, in the field names that work: MEASURED SIT2 2026-09-29, a PUT on the
    /// SR's <c>ICMContact</c> child with <c>Id</c>, <c>SR Relationship</c> and
    /// <c>SSA Primary Field</c> links the contact, sets the SR's <c>Primary Contact Id</c>
    /// when primary, and fills the SR header's names — the same shape the bus pass
    /// workflow leaves on a matched SR. The guidance's own <c>Relationship</c> and
    /// <c>Subject</c> names are accepted and silently ignored.
    /// </para>
    /// <para>
    /// The contact must already exist: this links, it does not create. Find the contact
    /// with <see cref="Services.IContactService"/> first.
    /// </para>
    /// </remarks>
    public class ServiceRequestContactLink
    {
        /// <summary>Gets or sets the contact's row id — <see cref="Contact.Id"/>. Required.</summary>
        public string? ContactId { get; set; }

        /// <summary>Gets or sets <c>SR Relationship</c>; null sends <c>Key player</c>, what the workflow files for the applicant.</summary>
        public string? Relationship { get; set; }

        /// <summary>Gets or sets <c>SSA Primary Field</c>; null sends true. The primary contact becomes the SR's <c>Primary Contact Id</c>.</summary>
        public bool? IsPrimary { get; set; }

        /// <summary>Gets or sets <c>SR Subject Flag</c>; null sends nothing (ICM defaults it to <c>N</c>).</summary>
        public bool? IsSubject { get; set; }
    }
}
