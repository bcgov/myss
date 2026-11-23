namespace Myss.Api.Tests.TestDoubles
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;
    using Myss.Api.Services;
    using Myss.Data;
    using Myss.FormSpec;

    /// <summary>
    /// An <see cref="IAccountCreationService"/> that reports no errors and
    /// records nothing, for tests of forms other than registration.
    /// </summary>
    public sealed class FakeAccountCreationService : IAccountCreationService
    {
        /// <inheritdoc/>
        public Task<IReadOnlyList<ValidationErrorModel>> ValidateAsync(
            AccountCreationRequestModel? model,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ValidationErrorModel>>([]);

        /// <inheritdoc/>
        public void Apply(MyssUserProfile profile, AccountCreationRequestModel model)
        {
        }
    }
}
