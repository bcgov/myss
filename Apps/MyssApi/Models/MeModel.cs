namespace Myss.Api.Models
{
    using Myss.Common.Auth;

    /// <summary>
    /// The citizen API's answer to <c>GET /v1/auth/me</c>: the caller's identity and
    /// whether they have completed MySS registration.
    /// </summary>
    public sealed record MeModel : CurrentUser
    {
        /// <summary>Initializes a new instance of the <see cref="MeModel"/> class.</summary>
        /// <param name="user">The caller's identity.</param>
        public MeModel(CurrentUser user)
            : base(user)
        {
        }

        /// <summary>Gets a value indicating whether the caller has completed MySS registration.</summary>
        public bool HasProfile { get; init; }

        /// <summary>Gets the caller's first name as stored in their MySS profile.</summary>
        public string? ProfileFirstName { get; init; }
    }
}
