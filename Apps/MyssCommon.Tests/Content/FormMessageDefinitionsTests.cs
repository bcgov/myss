namespace Myss.Common.Tests.Content
{
    using System.Text.RegularExpressions;
    using Myss.Common.Content;

    /// <summary>
    /// Tests for <see cref="FormMessageDefinitions"/>: every definition can be stored,
    /// found and checked, and nothing else is found.
    /// </summary>
    public sealed partial class FormMessageDefinitionsTests
    {
        public static TheoryData<FormMessageDefinition> Definitions =>
            new(FormMessageDefinitions.All);

        [Theory]
        [MemberData(nameof(Definitions))]
        public void EachDefinition_HasAFormIdAndKeyTheContentEngineAccepts(FormMessageDefinition definition)
        {
            Assert.Matches(StoredName(), definition.FormSpecId);
            Assert.Matches(StoredName(), definition.MessageKey);
            Assert.InRange(definition.MessageKey.Length, 1, 50);
            Assert.False(string.IsNullOrWhiteSpace(definition.Name));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
        }

        [Theory]
        [MemberData(nameof(Definitions))]
        public void EachPlaceholder_IsOneThePageReplaces_WithADescriptionAndSample(FormMessageDefinition definition)
        {
            Assert.All(definition.Placeholders, placeholder =>
            {
                Assert.Matches(PlaceholderName(), placeholder.Name);
                Assert.False(string.IsNullOrWhiteSpace(placeholder.Description));
                Assert.False(string.IsNullOrWhiteSpace(placeholder.Sample));
            });
            Assert.Equal(
                definition.Placeholders.Count,
                definition.Placeholders.Select(placeholder => placeholder.Name).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void NoTwoDefinitions_ShareAFormIdAndKey()
        {
            Assert.Equal(
                FormMessageDefinitions.All.Count,
                FormMessageDefinitions.All.Select(definition => (definition.FormSpecId, definition.MessageKey)).Distinct().Count());
        }

        [Fact]
        public void Find_ReturnsTheBusPassConfirmation_WithItsRequiredReferenceNumber()
        {
            FormMessageDefinition? definition = FormMessageDefinitions.Find("bc-bus-pass", "confirmation");

            Assert.NotNull(definition);
            FormMessagePlaceholder placeholder = Assert.Single(definition.Placeholders);
            Assert.Equal("referenceNumber", placeholder.Name);
            Assert.True(placeholder.Required);
        }

        [Theory]
        [InlineData("bc-bus-pass", "intro")]
        [InlineData("registration", "confirmation")]
        [InlineData("BC-BUS-PASS", "confirmation")]
        [InlineData("bc-bus-pass", "Confirmation")]
        public void Find_ReturnsNull_ForAnyOtherFormOrKey(string formSpecId, string messageKey)
        {
            Assert.Null(FormMessageDefinitions.Find(formSpecId, messageKey));
        }

        [GeneratedRegex("^[a-z0-9-]+$")]
        private static partial Regex StoredName();

        [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$")]
        private static partial Regex PlaceholderName();
    }
}
