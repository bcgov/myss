namespace Myss.AdminApi.Tests.Controllers
{
    using System.Net;
    using System.Reflection;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Myss.AdminApi.Configuration;
    using Myss.AdminApi.Controllers;
    using Myss.AdminApi.Domain;
    using Myss.AdminApi.Models;
    using Myss.AdminApi.Providers;
    using Myss.AdminApi.Services;
    using Myss.AdminApi.Tests.TestSupport;
    using Myss.FormSpec;

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
        public async Task Get_WrapsTheTableInTheEnvelope()
        {
            ActionResult<BaseResponseModel<EligibilityRatesModel>> result =
                await new EligibilityRatesController(NullLogger<EligibilityRatesController>.Instance, _service)
                    .Get(CancellationToken.None);

            Assert.NotNull(result.Value);
            Assert.Same(_service.Rates, result.Value!.Payload);
        }

        [Fact]
        public async Task Get_AnUnreadableContentEngine_Returns502()
        {
            _service.Exception = new ContentEngineUnavailableException("down");

            ActionResult<BaseResponseModel<EligibilityRatesModel>> result =
                await new EligibilityRatesController(NullLogger<EligibilityRatesController>.Instance, _service)
                    .Get(CancellationToken.None);

            Assert.Equal(502, Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);
            Assert.Null(result.Value);
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
            public EligibilityRatesModel Rates { get; set; } = EligibilityRatesTestData.SeededTable("2026-10-02");

            public EligibilityRatesWriteResultModel? Result { get; set; }

            public Task<EligibilityRatesModel> GetAsync(CancellationToken cancellationToken) =>
                Exception is not null
                    ? Task.FromException<EligibilityRatesModel>(Exception)
                    : Task.FromResult(Rates);

            public Exception? Exception { get; set; }

            public Task<EligibilityRatesWriteResultModel> SaveAsync(
                SaveEligibilityRatesRequestModel request, CancellationToken cancellationToken) =>
                Exception is not null
                    ? Task.FromException<EligibilityRatesWriteResultModel>(Exception)
                    : Task.FromResult(Result ?? throw new InvalidOperationException("No result configured."));
        }
    }
}
