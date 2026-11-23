namespace Myss.Api.Services
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;
    using Myss.Data;
    using Myss.FormSpec;

    /// <summary>
    /// Checks and records who a registration makes the account for: the context,
    /// the helper's authorization, the representative and the linked documents.
    /// </summary>
    public interface IAccountCreationService
    {
        /// <summary>
        /// Checks the account-creation answers of a registration by the current
        /// user. A user who already has a profile gets no errors: the answers are
        /// recorded only when the profile is created.
        /// </summary>
        /// <param name="model">The answers sent, or null when none were.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Every failure; empty when the answers pass or are not needed.</returns>
        Task<IReadOnlyList<ValidationErrorModel>> ValidateAsync(
            AccountCreationRequestModel? model,
            CancellationToken cancellationToken);

        /// <summary>
        /// Records answers that passed <see cref="ValidateAsync"/> on a profile
        /// being created, in the same unit of work as the profile.
        /// </summary>
        /// <param name="profile">The new profile.</param>
        /// <param name="model">The checked answers.</param>
        void Apply(MyssUserProfile profile, AccountCreationRequestModel model);
    }
}
