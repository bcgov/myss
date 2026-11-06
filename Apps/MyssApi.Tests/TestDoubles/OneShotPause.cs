namespace Myss.Api.Tests.TestDoubles
{
    /// <summary>
    /// Holds the next caller that reaches it until the test releases it, so a
    /// race between two requests can be played out in a fixed order. Only the
    /// first caller after <see cref="Arm"/> is held; later ones pass straight
    /// through.
    /// </summary>
    public sealed class OneShotPause
    {
        private Gate? _gate;

        /// <summary>Arms the pause for the next caller.</summary>
        /// <returns>A task that completes once a caller is held, and the source that releases it.</returns>
        public (Task Reached, TaskCompletionSource Release) Arm()
        {
            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _gate = new Gate(reached, release);
            return (reached.Task, release);
        }

        /// <summary>Holds the caller if the pause is armed; otherwise returns at once.</summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes once the caller may go on.</returns>
        public async Task WaitIfArmedAsync(CancellationToken cancellationToken)
        {
            Gate? gate = Interlocked.Exchange(ref _gate, null);
            if (gate is not null)
            {
                gate.Reached.TrySetResult();
                await gate.Release.Task.WaitAsync(cancellationToken);
            }
        }

        private sealed record Gate(TaskCompletionSource Reached, TaskCompletionSource Release);
    }
}
