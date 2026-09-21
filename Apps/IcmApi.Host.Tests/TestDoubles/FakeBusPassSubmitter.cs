namespace Icm.Api.Host.Tests.TestDoubles
{
    using Icm.Api.Host.Services;
    using Icm.Api.Models;

    /// <summary>A submitter that records what it was given and answers as told.</summary>
    public sealed class FakeBusPassSubmitter : IBusPassSubmitter
    {
        public List<BusPassApplication> Submitted { get; } = [];

        public BusPassResult Result { get; set; } = Accepted("1-TEST-0001");

        public Exception? Failure { get; set; }

        public static BusPassResult Accepted(string applicationNumber, string? firstName = "Myss", string? lastName = "IntegrationTest") =>
            new() { ApplicationNumber = applicationNumber, Status = "SUCCESS", FirstName = firstName, LastName = lastName };

        public static BusPassResult Rejected(string applicationNumber, string errorCode, string errorMessage) =>
            new() { ApplicationNumber = applicationNumber, ErrorCode = errorCode, ErrorMessage = errorMessage, Status = "Error" };

        public Task<BusPassResult> SubmitAsync(BusPassApplication application, CancellationToken cancellationToken)
        {
            Submitted.Add(application);
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(Result);
        }
    }
}
