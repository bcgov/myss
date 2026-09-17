namespace Icm.Api.Host.Services
{
    /// <summary>
    /// Resolves the correlation id of the current request.
    /// </summary>
    public interface ICorrelationIdAccessor
    {
        /// <summary>
        /// Gets the id that identifies the current request across MyssApi, this
        /// host and ICM. Null outside a request.
        /// </summary>
        string? CorrelationId { get; }
    }
}
