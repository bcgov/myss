namespace Myss.Api.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// The typed identity of the caller for the current request.
    /// <para>
    /// PERMANENT core: identical under Option 1 (bearer token) and Option 2 (session cookie).
    /// Services depend on this rather than on <c>HttpContext.User</c> so they stay unaware of
    /// how the caller was authenticated.
    /// </para>
    /// </summary>
    public sealed record CurrentUser
    {
        /// <summary>Gets the anonymous caller singleton.</summary>
        public static CurrentUser Anonymous { get; } = new();

        /// <summary>Gets a value indicating whether the caller is authenticated.</summary>
        public bool IsAuthenticated { get; init; }

        /// <summary>Gets the Keycloak subject identifier (<c>sub</c>).</summary>
        public string Subject { get; init; } = string.Empty;

        /// <summary>Gets the caller's roles, flattened by <see cref="Configuration.KeycloakClaims"/>.</summary>
        public IReadOnlyList<string> Roles { get; init; } = [];

        /// <summary>Gets the Basic BCeID user GUID, when the caller signed in with BCeID.</summary>
        public string? BceidGuid { get; init; }

        /// <summary>Gets the IDIR username, when the caller is government staff.</summary>
        public string? IdirUsername { get; init; }
    }
}
