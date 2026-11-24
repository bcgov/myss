namespace Myss.FormSpec.Tests.TestSupport
{
    using System.Text.Json;

    /// <summary>
    /// Reads <c>Shared/validation/channel-specs.json</c>, the cases MyssContent's
    /// publish rule and MyssFormio's editor check and preview also run.
    /// </summary>
    public static class ChannelCases
    {
        private static readonly JsonDocument Document = Load();

        /// <summary>Gets the channel list every copy must equal.</summary>
        public static IReadOnlyList<string> Channels =>
            [.. Document.RootElement.GetProperty("channels").EnumerateArray().Select(c => c.GetString()!)];

        /// <summary>Gets the names of the cases in one section.</summary>
        /// <param name="section">"publishCases" or "resolveCases".</param>
        /// <returns>The names, for xUnit's member data.</returns>
        public static TheoryData<string> Names(string section)
        {
            TheoryData<string> names = [];
            foreach (JsonElement item in Document.RootElement.GetProperty(section).EnumerateArray())
            {
                names.Add(item.GetProperty("name").GetString()!);
            }

            return names;
        }

        /// <summary>Gets one case by name.</summary>
        /// <param name="section">"publishCases" or "resolveCases".</param>
        /// <param name="name">The case's name.</param>
        /// <returns>The case.</returns>
        public static JsonElement Case(string section, string name) =>
            Document.RootElement.GetProperty(section).EnumerateArray()
                .Single(c => c.GetProperty("name").GetString() == name);

        private static JsonDocument Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "channel-specs.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The shared channel cases were not copied beside the test assembly. Check the "
                    + "<Content Include=\"..\\..\\Shared\\validation\\channel-specs.json\"> item in the csproj.",
                    path);
            }

            return JsonDocument.Parse(File.ReadAllText(path));
        }
    }
}
