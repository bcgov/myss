namespace Myss.Api.Services
{
    /// <summary>
    /// The correlation id of the request being served, so one citizen action
    /// can be followed through logs, stored records and outbound calls to the
    /// ICM middleware (handbook Part 4.12).
    /// </summary>
    public interface ICorrelationIdAccessor
    {
        /// <summary>
        /// Gets the id for the current request, or null when there is no request,
        /// such as a background job or a test.
        /// </summary>
        string? CorrelationId { get; }
    }
}
