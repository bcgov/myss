namespace Myss.Api.Data
{
    using System;

    /// <summary>
    /// What happened at one point in an attempt to hand a bus pass submission
    /// to ICM.
    /// </summary>
    public enum BusPassDispatchEventType
    {
        /// <summary>
        /// The call to the middleware is about to be made. A started event with
        /// no later event for the same attempt means the process died mid-call:
        /// ICM may already have the request, so it must never be re-sent
        /// automatically.
        /// </summary>
        Started,

        /// <summary>ICM accepted the request and assigned a reference number.</summary>
        Accepted,

        /// <summary>ICM received the request and declined it.</summary>
        Rejected,

        /// <summary>No outcome could be obtained from the middleware.</summary>
        Failed,
    }

    /// <summary>
    /// One event in the append-only log of hand-offs to ICM for a bus pass
    /// submission. Rows are inserted and never updated: an attempt is a started
    /// event followed, when the call returns, by exactly one accepted, rejected
    /// or failed event sharing its attempt id. The history of a submission is
    /// the list of its events, so nothing that happened is lost to a status
    /// column overwritten in place, and the audit trail and the state are the
    /// same rows.
    /// </summary>
    public class BusPassDispatchEvent
    {
        /// <summary>Gets or sets the event identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Gets or sets the submission the attempt belongs to.</summary>
        public Guid SubmissionId { get; set; }

        /// <summary>Gets or sets the attempt this event is part of; the started and the closing event share it.</summary>
        public Guid AttemptId { get; set; }

        /// <summary>Gets or sets what happened.</summary>
        public BusPassDispatchEventType Type { get; set; }

        /// <summary>Gets or sets when it happened.</summary>
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>
        /// Gets or sets the correlation id of the request that caused the event,
        /// the same <c>X-Request-ID</c> that went to the middleware, so a row can
        /// be matched to its trace.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>Gets or sets the reference number ICM assigned, when it did.</summary>
        public string? ReferenceNumber { get; set; }

        /// <summary>Gets or sets ICM's error code for a rejection.</summary>
        public string? ErrorCode { get; set; }

        /// <summary>Gets or sets the error text, from ICM or from the transport failure.</summary>
        public string? ErrorMessage { get; set; }
    }
}
