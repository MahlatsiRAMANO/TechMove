// Run test classes one after another. The integration tests host the API in-process, and the
// shared fixture / database should not be hit by several test classes at the same time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
