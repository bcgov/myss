namespace Myss.FormSpec.Tests
{
    using Myss.FormSpec.Tests.TestSupport;

    public class RequestChannelsTests
    {
        [Fact]
        public void All_EqualsTheSharedList()
        {
            Assert.Equal(ChannelCases.Channels, RequestChannels.All);
        }

        [Theory]
        [InlineData("client", true)]
        [InlineData("worker-override", true)]
        [InlineData("Client", false)]
        [InlineData("applicant-override", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsKnown_ComparesExactly(string? channel, bool expected)
        {
            Assert.Equal(expected, RequestChannels.IsKnown(channel));
        }
    }
}
