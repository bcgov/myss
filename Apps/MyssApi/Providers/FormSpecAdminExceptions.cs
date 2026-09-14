namespace Myss.Api.Providers
{
    using System;
    using System.Net;

    /// <summary>
    /// Thrown when the content engine refuses a form-spec write. Carries Strapi's
    /// HTTP status and raw error body so the caller can surface the reason (for
    /// example, a lifecycle validation refusal) rather than a generic failure.
    /// </summary>
    public class StrapiWriteException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StrapiWriteException"/> class.
        /// </summary>
        /// <param name="statusCode">The HTTP status Strapi returned.</param>
        /// <param name="body">The raw response body Strapi returned.</param>
        public StrapiWriteException(HttpStatusCode statusCode, string? body)
            : base($"Strapi refused the form-spec write with {(int)statusCode}.")
        {
            this.StatusCode = statusCode;
            this.Body = body;
        }

        /// <summary>
        /// Gets the HTTP status the content engine returned.
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Gets the raw response body the content engine returned, if any.
        /// </summary>
        public string? Body { get; }
    }

    /// <summary>
    /// Thrown when the content engine cannot be reached, times out, returns a
    /// non-success status on a read, or returns a malformed response - an
    /// infrastructure/configuration failure rather than a business refusal. The API
    /// maps it to 502 with a generic message; the detail is logged, not returned.
    /// </summary>
    public class ContentEngineUnavailableException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="ContentEngineUnavailableException"/> class.</summary>
        /// <param name="message">A description of the failure.</param>
        public ContentEngineUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="ContentEngineUnavailableException"/> class.</summary>
        /// <param name="message">A description of the failure.</param>
        /// <param name="innerException">The underlying transport or parse failure.</param>
        public ContentEngineUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Thrown by the admin provider when a publish is requested but the form has no
    /// in-progress (never-published) draft to release. A distinct type so the service
    /// catches ONLY this case, never an unrelated <see cref="System.InvalidOperationException"/>
    /// (for example one raised while parsing a malformed upstream response).
    /// </summary>
    public class NoDraftToPublishException : Exception
    {
        /// <summary>Initializes a new instance of the <see cref="NoDraftToPublishException"/> class.</summary>
        /// <param name="formSpecId">The form that had no in-progress draft.</param>
        public NoDraftToPublishException(string formSpecId)
            : base($"No in-progress draft to publish for form '{formSpecId}'. Save a draft before publishing.")
        {
            this.FormSpecId = formSpecId;
        }

        /// <summary>Gets the form that had no in-progress draft.</summary>
        public string FormSpecId { get; }
    }
}
