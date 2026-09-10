namespace Myss.Api.Data
{
    using System;

    /// <summary>
    /// How one attempt to hand a bus pass submission to ICM ended.
    /// </summary>
    public enum BusPassDispatchOutcome
    {
        /// <summary>
        /// The call is in flight, or the process died during it. A pending row
        /// means ICM may already have the request, so it must never be sent
        /// again automatically.
        /// </summary>
        Pending,

        /// <summary>ICM accepted the request and assigned a reference number.</summary>
        Accepted,

        /// <summary>ICM received the request and declined it.</summary>
        Rejected,

        /// <summary>No outcome could be obtained from the middleware.</summary>
        Failed,
    }

    /// <summary>
    /// One attempt to deliver a stored bus pass submission to ICM through the
    /// middleware. Append-only: every attempt is its own row, so the history of
    /// a submission is the list of its dispatches rather than a status column
    /// that gets overwritten.
    /// </summary>
    public class BusPassDispatch
    {
        /// <summary>Gets or sets the dispatch identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the submission this attempt belongs to.</summary>
        public Guid SubmissionId { get; set; }

        /// <summary>Gets or sets when the attempt started.</summary>
        public DateTimeOffset AttemptedAt { get; set; }

        /// <summary>Gets or sets when the attempt reached a final outcome.</summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>Gets or sets the outcome.</summary>
        public BusPassDispatchOutcome Outcome { get; set; }

        /// <summary>Gets or sets the reference number ICM assigned, when it did.</summary>
        public string? ReferenceNumber { get; set; }

        /// <summary>Gets or sets ICM's error code for a rejection.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Gets or sets the error text, from ICM or from the transport failure.</summary>
        public string? ErrorMessage { get; set; }
    }
}
