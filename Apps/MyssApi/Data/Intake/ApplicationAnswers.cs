namespace Myss.Api.Intake
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// An Income Assistance application: its identity, its owner, the form
    /// spec version it was started on, and its answers as a working copy
    /// while it is being drafted (handbook Part 7.2's
    /// <c>intake.application_answers</c>, plus owner, row version and
    /// timestamps).
    /// </summary>
    /// <remarks>
    /// The row never says what state the application is in. State is a fold
    /// over the application's event stream in the platform log, keyed by
    /// <see cref="Id"/>. Once that fold says Submitted, the answers that count
    /// are the ones in the Submitted event's payload, and this row's copy is
    /// no longer read.
    /// </remarks>
    public class ApplicationAnswers
    {
        /// <summary>
        /// Gets or sets the application identifier. It is also the event stream id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the owner's Keycloak <c>sub</c> claim. Identity stays
        /// in Keycloak, so there is no local users table to join against.
        /// </summary>
        public required string OwnerSubject { get; set; }

        /// <summary>
        /// Gets or sets the logical form identifier.
        /// </summary>
        public required string FormSpecId { get; set; }

        /// <summary>
        /// Gets or sets the spec version pinned when the application was
        /// created. A later publish does not move a draft; submit stamps this
        /// version, and every render, draft or submitted, uses it.
        /// </summary>
        public int FormSpecVersion { get; set; }

        /// <summary>
        /// Gets or sets the draft answers (jsonb).
        /// </summary>
        public required JsonDocument Answers { get; set; }

        /// <summary>
        /// Gets or sets the row version: 1 at create, +1 per save. It is an EF
        /// concurrency token, so only the client holding the latest version
        /// can save; a stale tab gets a 409 instead of silently overwriting.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets when the application was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the answers were last saved.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
