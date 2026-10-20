namespace Myss.AdminApi.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Services;

    /// <summary>
    /// The staff reads of stored form submissions, moved from MyssApi with the
    /// rest of the worker side. Same routes as before, under <c>v1/forms</c> beside
    /// the form editor's <see cref="FormsController"/>, but a separate controller:
    /// the editor is open to any IDIR user (AdminIdir), while submissions hold
    /// citizens' answers and need the WORKER or ADMIN role as well (WorkerWithIdir).
    /// </summary>
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/forms")]
    [ApiController]
    [Authorize(Policy = MyssPolicies.WorkerWithIdir)]
    public class FormSubmissionsController : Controller
    {
        private readonly IFormSubmissionsService _submissionsService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FormSubmissionsController"/> class.
        /// </summary>
        /// <param name="submissionsService">Injected submissions service.</param>
        public FormSubmissionsController(IFormSubmissionsService submissionsService)
        {
            _submissionsService = submissionsService;
        }

        /// <summary>
        /// Lists a form's submissions, newest first (metadata only).
        /// </summary>
        /// <param name="formSpecId">The logical form identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("{formSpecId}/submissions")]
        [Produces("application/json")]
        [EndpointName("ListFormSubmissions")]
        [ProducesResponseType(
            typeof(BaseResponseModel<IReadOnlyList<FormSubmissionSummaryModel>>),
            StatusCodes.Status200OK
        )]
        public async Task<
            ActionResult<BaseResponseModel<IReadOnlyList<FormSubmissionSummaryModel>>>
        > ListSubmissions(string formSpecId, CancellationToken cancellationToken)
        {
            IReadOnlyList<FormSubmissionSummaryModel> submissions =
                await _submissionsService.ListSubmissionsAsync(formSpecId, cancellationToken);
            return new BaseResponseModel<IReadOnlyList<FormSubmissionSummaryModel>>
            {
                Payload = submissions,
                DatetimeRequested = DateTime.Now,
            };
        }

        /// <summary>
        /// Returns a submission with the archived spec version that rendered it.
        /// </summary>
        /// <param name="id">The submission identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        [HttpGet("submissions/{id:guid}")]
        [Produces("application/json")]
        [EndpointName("GetFormSubmission")]
        [ProducesResponseType(
            typeof(BaseResponseModel<FormSubmissionResponseModel>),
            StatusCodes.Status200OK
        )]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<
            ActionResult<BaseResponseModel<FormSubmissionResponseModel>>
        > GetSubmission(Guid id, CancellationToken cancellationToken)
        {
            FormSubmissionResponseModel? submission = await _submissionsService.GetSubmissionAsync(
                id,
                cancellationToken
            );
            if (submission is null)
            {
                return NotFound();
            }

            return new BaseResponseModel<FormSubmissionResponseModel>
            {
                Payload = submission,
                DatetimeRequested = DateTime.Now,
            };
        }
    }
}
