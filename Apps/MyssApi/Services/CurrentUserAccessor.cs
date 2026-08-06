namespace Myss.Api.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using Microsoft.AspNetCore.Http;
    using Myss.Api.Configuration;
    using Myss.Api.Models;

    /// <summary>
    /// Reads the caller's identity off <c>HttpContext.User</c>.
    /// PERMANENT core: works with any authentication scheme that produced the principal,
    /// so it is unchanged when the app moves from Option 1 to Option 2.
    /// </summary>
    public class CurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        /// <summary>Initializes a new instance of the <see cref="CurrentUserAccessor"/> class.</summary>
        /// <param name="httpContextAccessor">The injected HTTP context accessor.</param>
        public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public CurrentUser User => FromPrincipal(this.httpContextAccessor.HttpContext?.User);

        /// <summary>
        /// Projects a claims principal onto <see cref="CurrentUser"/>. Exposed as a static so it
        /// can be unit tested without an HTTP context.
        /// </summary>
        /// <param name="principal">The principal to read, may be null.</param>
        /// <returns>The typed caller, or <see cref="CurrentUser.Anonymous"/>.</returns>
        public static CurrentUser FromPrincipal(ClaimsPrincipal? principal)
        {
            if (principal?.Identity is null || !principal.Identity.IsAuthenticated)
            {
                return CurrentUser.Anonymous;
            }

            IReadOnlyList<string> roles = principal
                .FindAll(KeycloakClaims.RolesClaimType)
                .Select(c => c.Value)
                .Distinct(System.StringComparer.Ordinal)
                .ToArray();

            return new CurrentUser
            {
                IsAuthenticated = true,
                Subject = principal.FindFirst("sub")?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? string.Empty,
                Roles = roles,
                BceidGuid = principal.FindFirst(KeycloakClaims.BceidGuidClaimType)?.Value,
                IdirUsername = principal.FindFirst(KeycloakClaims.IdirUsernameClaimType)?.Value,
            };
        }
    }
}
