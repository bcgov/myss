namespace Myss.Api.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The ICM contact match a new registration goes through: does ICM already
    /// know the citizen as a contact?
    /// </summary>
    /// <remarks>
    /// Not connected yet, so <see cref="HardcodedContactMatchProvider"/> stands
    /// in and always finds a match. Connecting it is a new implementation of
    /// this interface over the IcmApi contact search, which already takes every
    /// criterion here; registration does not change.
    /// </remarks>
    public interface IContactMatchProvider
    {
        /// <summary>
        /// Looks for the citizen among ICM's contacts.
        /// </summary>
        /// <param name="criteria">The identity attributes from registration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether a matching contact was found.</returns>
        Task<ContactMatchResult> MatchAsync(ContactMatchCriteria criteria, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The identity attributes a contact is matched on.
    /// </summary>
    /// <param name="FirstName">The first name.</param>
    /// <param name="LastName">The last name.</param>
    /// <param name="DateOfBirth">The date of birth.</param>
    /// <param name="Email">The email address.</param>
    /// <param name="Sin">The social insurance number, nine digits.</param>
    public sealed record ContactMatchCriteria(
        string FirstName,
        string LastName,
        DateOnly DateOfBirth,
        string Email,
        string Sin);

    /// <summary>
    /// The outcome of a contact match.
    /// </summary>
    /// <param name="IsMatch">Whether ICM holds a contact for the citizen.</param>
    public sealed record ContactMatchResult(bool IsMatch);
}
