namespace Myss.Api.Tests.Controllers
{
    using System.Net;
    using System.Reflection;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.Api.Configuration;
    using Myss.Api.Controllers;
    using Myss.Api.Domain;
    using Myss.Api.Models;
    using Myss.Api.Providers;
    using Myss.Api.Services;
    using Myss.Api.Tests.TestSupport;

    /// <summary>
    /// Response-shaping and authorization-attribute tests for
    /// <see cref="EligibilityRatesController"/>. The real pipeline is covered by
    /// EligibilityRatesEndpointTests.
    /// </summary>
    public class EligibilityRatesControllerTests
    {
        private readonly StubRatesService _service = new();

        [Fact]
        public async Task AnAcceptedSave_WrapsTheSavedTableInTheEnvelope()
        {
            _service.Result = EligibilityRatesWriteResultModel.Accepted(EligibilityRatesTestData.SeededTable("2026-10-06"));

            ActionResult<BaseResponseModel<EligibilityRatesModel>> result = await Save();

            Assert.Equal("2026-10-06", result.Value!.Payload.EffectiveDate);
        }

        [Fact]
        public async Task ARefusedSave_Returns422_WithEveryError()
        {
            ValidationErrorModel[] errors =
            [
                new() { Field = "incomeRows.2.b", Keyword = EligibilityRateKeywords.AmountNegative, Message = "x" },
                new() { Field = "assetLimits.c", Keyword = EligibilityRateKeywords.AmountTooPrecise, Message = "y" },
            ];
            _service.Result = EligibilityRatesWriteResultModel.Refused(errors);

            ActionResult<BaseResponseModel<EligibilityRatesModel>> result = await Save();

            UnprocessableEntityObjectResult unprocessable = Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
            BaseResponseModel<IReadOnlyList<ValidationErrorModel>> body =
                Assert.IsType<BaseResponseModel<IReadOnlyList<ValidationErrorModel>>>(unprocessable.Value);
            Assert.Equal(["incomeRows.2.b", "assetLimits.c"], body.Payload.Select(error => error.Field));
        }

        [Fact]
        public async Task AFailedWrite_Returns502()
        {
            _service.Exception = new StrapiWriteException(HttpStatusCode.Forbidden, "forbidden");

            ActionResult<BaseResponseModel<EligibilityRatesModel>> result = await Save();

            Assert.Equal(502, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        }

        [Fact]
        public async Task AnUnavailableContentEngine_Returns502()
        {
            _service.Exception = new ContentEngineUnavailableException("down");

            ActionResult<BaseResponseModel<EligibilityRatesModel>> result = await Save();

            Assert.Equal(502, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
        }

        [Fact]
        public void TheController_RequiresTheAdminIdirPolicy_AndNothingAllowsAnonymous()
        {
            // Direct calls skip authorization, so only the attributes show the endpoint is protected.
            Type controller = typeof(EligibilityRatesController);

            Assert.Contains(
                controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true),
                attribute => attribute.Policy == MyssPolicies.AdminIdir);
            Assert.Empty(controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true));
            Assert.All(
                controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                method => Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true)));
        }

        private Task<ActionResult<BaseResponseModel<EligibilityRatesModel>>> Save() =>
            new EligibilityRatesController(NullLogger<EligibilityRatesController>.Instance, _service)
                .Save(EligibilityRatesTestData.SeededRequest(), CancellationToken.None);

        private sealed class StubRatesService : IEligibilityRatesService
        {
            public EligibilityRatesWriteResultModel? Result { get; set; }

            public Exception? Exception { get; set; }

            public Task<EligibilityRatesWriteResultModel> SaveAsync(
                SaveEligibilityRatesRequestModel request, CancellationToken cancellationToken) =>
                Exception is not null
                    ? Task.FromException<EligibilityRatesWriteResultModel>(Exception)
                    : Task.FromResult(Result ?? throw new InvalidOperationException("No result configured."));
        }
    }
}
