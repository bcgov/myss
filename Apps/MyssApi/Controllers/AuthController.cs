namespace Myss.Api.Controllers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// The auth surface of the API. <c>/me</c> is the SPA's single source of the caller's
    /// effective identity: roles here have been through <c>RoleCalculator</c> (ADR-0007),
    /// which the browser cannot reproduce — it cannot see the derive switch today, nor MySS
    /// account state once the APPLICANT/CLIENT split lands. This is also the seam Option 2
    /// (BFF) re-backs the SPA session with.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/auth")]
    [ApiController]
    [Authorize]
    public class AuthController : Controller
    {
        private readonly ICurrentUserAccessor currentUser;
        private readonly IUserProfileService userProfileService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="currentUser">The injected caller accessor.</param>
        /// <param name="userProfileService">The registration-profile lookup service.</param>
        public AuthController(
            ICurrentUserAccessor currentUser,
            IUserProfileService userProfileService)
        {
            this.currentUser = currentUser;
            this.userProfileService = userProfileService;
        }

        /// <summary>
        /// Returns the caller's effective identity: subject, effective roles, and the
        /// keystone identifiers (BCeID GUID / IDIR username).
        /// </summary>
        /// <returns>The caller's identity.</returns>
        [HttpGet("me")]
        [Produces("application/json")]
        [EndpointName("GetMe")]
        [ProducesResponseType(typeof(BaseResponseModel<CurrentUser>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<BaseResponseModel<CurrentUser>>> GetMe(CancellationToken cancellationToken)
        {
            CurrentUser user = this.currentUser.User;
            bool hasProfile = await this.userProfileService.HasProfileAsync(user.Subject, cancellationToken);
            string? profileFirstName = hasProfile
                ? await this.userProfileService.GetFirstNameAsync(user.Subject, cancellationToken)
                : null;
            return new BaseResponseModel<CurrentUser>
            {
                Payload = user with { HasProfile = hasProfile, ProfileFirstName = profileFirstName },
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
