namespace Myss.AdminApi.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The client's details a worker override account is made from (MYSS-321),
    /// already validated: the six the simplified Application for Assistance asks for.
    /// </summary>
    /// <param name="FirstName">The first name.</param>
    /// <param name="LastName">The last name.</param>
    /// <param name="DateOfBirth">The date of birth.</param>
    /// <param name="Email">The email address, trimmed.</param>
    /// <param name="Phone">The phone number, ten digits.</param>
    /// <param name="Sin">The social insurance number, nine digits.</param>
    public sealed record WorkerOverrideClient(
        string FirstName,
        string LastName,
        DateOnly DateOfBirth,
        string Email,
        string Phone,
        string Sin);

    /// <summary>
    /// The forms module's side of a worker override (MYSS-321): the account a
    /// worker's submit makes for a client. Intake reaches the profile table only
    /// through this, as the citizen side does through its profile service
    /// (ADR-0008). No email goes to the client, nothing is sent to ICM, and
    /// nobody signs in to the account.
    /// </summary>
    public interface IWorkerOverrideAccountService
    {
        /// <summary>
        /// Makes the worker override account for <paramref name="subject"/>, or
        /// brings its details up to date when a returned application is
        /// submitted again.
        /// </summary>
        /// <param name="subject">A worker override subject (<c>ProfileOrigins.WorkerOverrideSubject</c>).</param>
        /// <param name="client">The client's details.</param>
        /// <param name="idir">The IDIR of the worker submitting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The account's identifier.</returns>
        Task<Guid> SaveAsync(string subject, WorkerOverrideClient client, string idir, CancellationToken cancellationToken);

        /// <summary>Finds the account for a worker override subject.</summary>
        /// <param name="subject">The subject.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The account's identifier, or null before the first submit.</returns>
        Task<Guid?> FindAsync(string subject, CancellationToken cancellationToken);
    }
}
