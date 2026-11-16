namespace Myss.Data
{
    using System;

    /// <summary>
    /// The person who made an account for someone else at registration: a legal
    /// representative or a parent. One per account at most.
    /// </summary>
    public class AccountRepresentative
    {
        /// <summary>Gets or sets the row identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the profile of the person represented.</summary>
        public Guid ProfileId { get; set; }

        /// <summary>Gets or sets which kind of representative this is.</summary>
        public RepresentativeType Type { get; set; }

        /// <summary>Gets or sets the representative's first name, trimmed.</summary>
        public required string FirstName { get; set; }

        /// <summary>Gets or sets the representative's last name, trimmed.</summary>
        public required string LastName { get; set; }

        /// <summary>Gets or sets the representative's phone number, ten digits with no punctuation.</summary>
        public required string Phone { get; set; }

        /// <summary>
        /// Gets or sets the legal representative's agency, trimmed. Null when
        /// none was given, and always for a parent.
        /// </summary>
        public string? Agency { get; set; }

        /// <summary>Gets or sets when the representative was recorded.</summary>
        public DateTimeOffset CreatedAt { get; set; }
    }
}
