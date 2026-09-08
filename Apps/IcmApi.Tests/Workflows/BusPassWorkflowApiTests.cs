namespace Icm.Api.Tests.Workflows
{
    using System.Net;
    using System.Text.Json;
    using Icm.Api;
    using Icm.Api.Tests.TestDoubles;
    using Icm.Api.Workflows;
    using Icm.Api.Workflows.Contracts;
    using Refit;

    /// <summary>
    /// Checks the requests <see cref="IBusPassWorkflowApi"/> actually puts on the wire
    /// against <c>docs/integration/BusPassWorkflow_OpenApi.json</c>. The path here is the
    /// novel part — a workflow's display name, spaces and all — so most of these are about
    /// the URL surviving intact.
    /// </summary>
    public class BusPassWorkflowApiTests
    {
        private static readonly Uri BaseAddress = new("https://icm.example.gov.bc.ca:8443/gov/v1.0");

        private static (IBusPassWorkflowApi Api, RecordingHttpMessageHandler Handler) CreateApi(
            HttpStatusCode statusCode = HttpStatusCode.OK,
            string? responseJson = "{}")
        {
            RecordingHttpMessageHandler handler = new(statusCode, responseJson);
            HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
            return (RestService.For<IBusPassWorkflowApi>(httpClient, IcmRefitSettings.Create()), handler);
        }

        [Fact]
        public async Task SubmitAsync_PostsToTheWorkflowPathWithItsSpacesEncodedAndItsTrailingSlashKept()
        {
            (IBusPassWorkflowApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SubmitAsync("t", null, new SiebelBusPassEnvelope());

            Assert.Equal(HttpMethod.Post, handler.Request!.Method);
            Assert.Equal(
                "/gov/v1.0/workflow/ICM%20Receive%20Bus%20Pass%20Online%20Request%20Wrapper%20WF/",
                handler.Request.RequestUri!.AbsolutePath);
        }

        [Fact]
        public async Task SubmitAsync_SendsTheBearerTokenAsAnAuthorizationHeader()
        {
            (IBusPassWorkflowApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SubmitAsync("token-abc", null, new SiebelBusPassEnvelope());

            Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
            Assert.Equal("token-abc", handler.Request.Headers.Authorization.Parameter);
        }

        [Fact]
        public async Task TheTrustedUserNameIsSentAsAnIcmHeaderAndOmittedWhenNull()
        {
            (IBusPassWorkflowApi withUser, RecordingHttpMessageHandler withUserHandler) = CreateApi();
            await withUser.SubmitAsync("t", "IDIR\\SOMEONE", new SiebelBusPassEnvelope());
            Assert.Equal(
                "IDIR\\SOMEONE",
                Assert.Single(withUserHandler.Request!.Headers.GetValues(
                    IBusPassWorkflowApi.TrustedUserNameHeader)));

            (IBusPassWorkflowApi without, RecordingHttpMessageHandler withoutHandler) = CreateApi();
            await without.SubmitAsync("t", null, new SiebelBusPassEnvelope());
            Assert.False(withoutHandler.Request!.Headers.Contains(
                IBusPassWorkflowApi.TrustedUserNameHeader));
        }

        [Fact]
        public async Task SubmitAsync_SerializesTheEnvelopeWithSiebelsNamesAndOmitsNulls()
        {
            (IBusPassWorkflowApi api, RecordingHttpMessageHandler handler) = CreateApi();

            await api.SubmitAsync(
                "t",
                null,
                new SiebelBusPassEnvelope
                {
                    SRInboundMessage = new SiebelBusPassMessage
                    {
                        IntObjectName = "ICMSRBusPassInboundIO",
                        ListOfICMSRBusPassInboundIO = new SiebelBusPassInboundList
                        {
                            ICMSRInbound =
                            [
                                new SiebelBusPassInbound
                                {
                                    ListOfPayload = new SiebelBusPassPayloadList
                                    {
                                        Payload =
                                        [
                                            new SiebelBusPassPayload
                                            {
                                                ListOfSRProspects = new SiebelBusPassProspectList
                                                {
                                                    SRProspects =
                                                    [
                                                        new SiebelBusPassProspect
                                                        {
                                                            HomePhone = "2505550100",
                                                            Unit = "4",
                                                            FstNme = "Pat",
                                                        },
                                                    ],
                                                },
                                            },
                                        ],
                                    },
                                },
                            ],
                        },
                    },
                });

            using JsonDocument body = JsonDocument.Parse(handler.RequestBody!);
            JsonElement prospect = body.RootElement
                .GetProperty("SRInboundMessage")
                .GetProperty("ListOfICMSRBusPassInboundIO")
                .GetProperty("ICMSRInbound")[0]
                .GetProperty("ListOfPayload")
                .GetProperty("Payload")[0]
                .GetProperty("ListOfSRProspects")
                .GetProperty("SRProspects")[0];

            // The # suffix is part of Siebel's field name, not punctuation to strip.
            Assert.Equal("2505550100", prospect.GetProperty("HomePhone#").GetString());
            Assert.Equal("4", prospect.GetProperty("Unit#").GetString());
            Assert.Equal("Pat", prospect.GetProperty("FstNme").GetString());

            // Unset properties are omitted, not sent as fifty nulls.
            Assert.False(prospect.TryGetProperty("SIN", out _));
            Assert.False(body.RootElement.GetProperty("SRInboundMessage")
                .TryGetProperty("MessageId", out _));
        }

        [Fact]
        public async Task TheExcludeEmptyFieldsFlagIsLowerCasedAndAbsentByDefault()
        {
            (IBusPassWorkflowApi byDefault, RecordingHttpMessageHandler defaultHandler) = CreateApi();
            await byDefault.SubmitAsync("t", null, new SiebelBusPassEnvelope());
            Assert.DoesNotContain(
                "excludeEmptyFieldsInResponse",
                defaultHandler.Request!.RequestUri!.Query,
                StringComparison.Ordinal);

            (IBusPassWorkflowApi withFlag, RecordingHttpMessageHandler flagHandler) = CreateApi();
            await withFlag.SubmitAsync("t", null, new SiebelBusPassEnvelope(), true);
            Assert.Contains(
                "excludeEmptyFieldsInResponse=true",
                flagHandler.Request!.RequestUri!.Query,
                StringComparison.Ordinal);
        }
    }
}
