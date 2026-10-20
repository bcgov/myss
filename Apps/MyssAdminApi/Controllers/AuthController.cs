namespace Myss.AdminApi.Controllers
{
    using System;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Services;

    /// <summary>
    /// Who the caller is, for the admin webclient: it shows the form editor to any
    /// IDIR user and the review screens to WORKER or ADMIN. Same route and envelope
    /// as MyssApi's <c>GET v1/auth/me</c>, without its MySS profile fields: staff
    /// have no citizen profile. The roles are the effective ones (after
    /// <see cref="Configuration.RoleCalculator"/>), the same set the policies test,
    /// so the screens shown match what the API will allow. The API still enforces
    /// every rule itself.
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/auth")]
    [ApiController]
    [Authorize]
    public class AuthController : Controller
    {
        private readonly ICurrentUserAccessor currentUser;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="currentUser">Injected current user accessor.</param>
        public AuthController(ICurrentUserAccessor currentUser)
        {
            this.currentUser = currentUser;
        }

        /// <summary>
        /// Returns the signed-in caller: subject, effective roles and identity details.
        /// </summary>
        [HttpGet("me")]
        [Produces("application/json")]
        [EndpointName("GetMe")]
        [ProducesResponseType(typeof(BaseResponseModel<CurrentUser>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public ActionResult<BaseResponseModel<CurrentUser>> Me()
        {
            return new BaseResponseModel<CurrentUser>
            {
                Payload = this.currentUser.User,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
