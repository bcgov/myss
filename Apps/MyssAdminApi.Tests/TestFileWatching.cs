namespace Myss.AdminApi.Tests
{
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Every WebApplicationFactory host loads its JSON config with reload on, and on
    /// Linux each of those file watchers takes an inotify instance. The suite builds
    /// hundreds of hosts (two per <c>CitizenAndStaffTestHost</c>), and the CI runner's
    /// limit is 128, so later hosts failed to build ("The configured user limit (128)
    /// on the number of inotify instances has been reached"). Polling watchers use no
    /// inotify instance, and no test relies on config reload.
    /// </summary>
    internal static class TestFileWatching
    {
        [ModuleInitializer]
        internal static void UsePollingFileWatchers() =>
            Environment.SetEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
    }
}
