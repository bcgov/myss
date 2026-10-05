namespace Myss.Api.Data
{
    using System;

    /// <summary>
    /// Registration details linked to the authenticated identity subject.
    /// </summary>
    public class MyssUserProfile
    {
        /// <summary>Gets or sets the profile identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the stable authenticated identity subject.</summary>
        public required string Subject { get; set; }

        /// <summary>Gets or sets the submitted first name.</summary>
        public required string FirstName { get; set; }

        /// <summary>Gets or sets the submitted last name.</summary>
        public required string LastName { get; set; }

        /// <summary>Gets or sets the submitted date of birth.</summary>
        public DateOnly DateOfBirth { get; set; }

        /// <summary>Gets or sets the submitted email address.</summary>
        public required string Email { get; set; }

        /// <summary>Gets or sets the submitted social insurance number.</summary>
        public required string Sin { get; set; }

        /// <summary>
        /// Gets or sets the submitted phone number, ten digits with no punctuation.
        /// Null for profiles registered before registration v4 asked for it.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Gets or sets the submitted gender, as the option value of the registration
        /// form's gender field. Null for profiles registered before registration v4.
        /// </summary>
        public string? Gender { get; set; }

        /// <summary>Gets or sets when the profile was first created.</summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>Gets or sets when the profile was last updated.</summary>
        public DateTimeOffset UpdatedAt { get; set; }
    }
}