namespace Icm.Api.Host.Tests.TestDoubles
{
    using Icm.Api.Models;
    using Icm.Api.Repositories;

    /// <summary>A repository that records the token and application and answers as told.</summary>
    public sealed class FakeBusPassRepository : IBusPassRepository
    {
        public List<(string Token, BusPassApplication Application)> Calls { get; } = [];

        public BusPassResult Result { get; set; } = new() { ApplicationNumber = "1-TEST-0001", Status = "SUCCESS" };

        public Exception? Failure { get; set; }

        public Task<BusPassResult> SubmitAsync(string bearerToken, BusPassApplication application, CancellationToken cancellationToken = default)
        {
            Calls.Add((bearerToken, application));
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(Result);
        }
    }
}
