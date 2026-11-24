namespace Myss.FormSpec
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// How an application was started. An application keeps its channel for
    /// life, and a form's channel rules (<c>properties.myssChannels</c>) are
    /// keyed by these values.
    /// </summary>
    public static class RequestChannels
    {
        /// <summary>The applicant started the application.</summary>
        public const string Client = "client";

        /// <summary>A worker started the application on the applicant's behalf.</summary>
        public const string WorkerOverride = "worker-override";

        /// <summary>Every channel, in the order the channel rules are checked.</summary>
        public static readonly IReadOnlyList<string> All = [Client, WorkerOverride];

        /// <summary>True when <paramref name="channel"/> is one of <see cref="All"/>, compared exactly.</summary>
        /// <param name="channel">The value to check.</param>
        /// <returns>Whether the value names a channel.</returns>
        public static bool IsKnown(string? channel) =>
            channel is not null && All.Contains(channel, StringComparer.Ordinal);
    }
}
