using Xunit;

// Disable parallel execution of test collections to prevent 
// global state (e.g., Hangfire) and Testcontainers port conflicts.
[assembly: CollectionBehavior(DisableTestParallelization = true)]