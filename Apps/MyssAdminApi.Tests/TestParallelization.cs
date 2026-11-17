using Xunit;

// The API's Serilog bootstrap assigns a process-wide static `Log.Logger`
// (ProgramConfiguration.GetInitialLogger, called from StartupConfiguration's
// constructor on every host build) which `UseSerilog` then freezes when the host
// finishes building. Two WebApplicationFactory hosts building in parallel race on
// that shared static and double-freeze it ("The logger is already frozen").
// Serialising the suite removes the race; it runs in a few seconds, so the cost is
// negligible. Remove this only if the shared-static-logger reset is fixed at the
// source (e.g. UseSerilog(..., preserveStaticLogger: true)).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
