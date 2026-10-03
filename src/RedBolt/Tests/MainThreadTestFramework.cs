// Copyright (c) 2026 RedBolt and contributors.
using System.Reflection;

using RedBolt.Core;

using Xunit.Abstractions;
using Xunit.Sdk;

[assembly: TestFramework("RedBolt.Tests.MainThreadTestFramework", "RedBolt.Tests")]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RedBolt.Tests;

/// <summary>
/// Treat the xUnit thread executing each test case as the engine main thread.
/// Collection fixtures may be constructed on a different managed thread.
/// </summary>
public sealed class MainThreadTestFramework : XunitTestFramework
{
    private static readonly MethodInfo SetMainThreadMethod = typeof(WorkQueue).GetMethod(
        "SetMainThread", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static int _mainThreadBoundaryCount;

    public MainThreadTestFramework(IMessageSink messageSink)
        : base(messageSink)
    {
        // Every test host must start with a fully registered process-local context.
        // Previously the first engine fixture happened to perform this initialization,
        // which made factory tests depend on xUnit's collection order.
        Context.SetInstance(Context.Instance);
    }

    public static int MainThreadBoundaryCount => Volatile.Read(ref _mainThreadBoundaryCount);

    internal static void EnterMainThreadBoundary()
    {
        Interlocked.Increment(ref _mainThreadBoundaryCount);
        SetMainThreadMethod.Invoke(null, null);
    }

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName) =>
        new MainThreadTestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
}

internal sealed class MainThreadTestFrameworkExecutor : XunitTestFrameworkExecutor
{
    public MainThreadTestFrameworkExecutor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : base(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
    }

    protected override void RunTestCases(
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
    {
        testCases = SelectShard(testCases);
        try
        {
            using var runner = new MainThreadTestAssemblyRunner(
                TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
            runner.RunAsync().GetAwaiter().GetResult();
        }
        finally
        {
            TestGraphicsCache.Delete();
        }
    }

    private static IEnumerable<IXunitTestCase> SelectShard(IEnumerable<IXunitTestCase> testCases)
    {
        var shardCountText = Environment.GetEnvironmentVariable("REDBOLT_TEST_SHARD_COUNT");
        if (string.IsNullOrEmpty(shardCountText))
            return testCases;

        if (!int.TryParse(shardCountText, out var shardCount) || shardCount < 1)
            throw new InvalidOperationException("REDBOLT_TEST_SHARD_COUNT must be a positive integer.");

        var shardIndexText = Environment.GetEnvironmentVariable("REDBOLT_TEST_SHARD_INDEX");
        if (!int.TryParse(shardIndexText, out var shardIndex) || shardIndex < 0 || shardIndex >= shardCount)
        {
            throw new InvalidOperationException(
                "REDBOLT_TEST_SHARD_INDEX must be between zero and REDBOLT_TEST_SHARD_COUNT minus one.");
        }

        return testCases.Where(testCase =>
            GetShardIndex(testCase.TestMethod.TestClass.Class.Name, shardCount) == shardIndex);
    }

    internal static int GetShardIndex(string testClassName, int shardCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(shardCount, 1);

        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        foreach (var character in testClassName)
        {
            hash ^= character;
            hash *= prime;
        }
        return (int)(hash % shardCount);
    }
}

internal sealed class MainThreadTestAssemblyRunner : XunitTestAssemblyRunner
{
    public MainThreadTestAssemblyRunner(
        ITestAssembly testAssembly,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageSink executionMessageSink,
        ITestFrameworkExecutionOptions executionOptions)
        : base(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
    {
    }

    protected override Task<RunSummary> RunTestCollectionAsync(
        IMessageBus messageBus,
        ITestCollection testCollection,
        IEnumerable<IXunitTestCase> testCases,
        CancellationTokenSource cancellationTokenSource) =>
        new MainThreadTestCollectionRunner(
            testCollection,
            testCases,
            DiagnosticMessageSink,
            messageBus,
            TestCaseOrderer,
            new ExceptionAggregator(Aggregator),
            cancellationTokenSource).RunAsync();
}

internal sealed class MainThreadTestCollectionRunner : XunitTestCollectionRunner
{
    public MainThreadTestCollectionRunner(
        ITestCollection testCollection,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        ITestCaseOrderer testCaseOrderer,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
        : base(
            testCollection,
            testCases,
            diagnosticMessageSink,
            messageBus,
            testCaseOrderer,
            aggregator,
            cancellationTokenSource)
    {
    }

    protected override Task<RunSummary> RunTestClassAsync(
        ITestClass testClass,
        IReflectionTypeInfo @class,
        IEnumerable<IXunitTestCase> testCases) =>
        new MainThreadTestClassRunner(
            testClass,
            @class,
            testCases,
            DiagnosticMessageSink,
            MessageBus,
            TestCaseOrderer,
            new ExceptionAggregator(Aggregator),
            CancellationTokenSource,
            CollectionFixtureMappings).RunAsync();
}

internal sealed class MainThreadTestClassRunner : XunitTestClassRunner
{
    public MainThreadTestClassRunner(
        ITestClass testClass,
        IReflectionTypeInfo @class,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        ITestCaseOrderer testCaseOrderer,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        IDictionary<Type, object> collectionFixtureMappings)
        : base(
            testClass,
            @class,
            testCases,
            diagnosticMessageSink,
            messageBus,
            testCaseOrderer,
            aggregator,
            cancellationTokenSource,
            collectionFixtureMappings)
    {
    }

    protected override Task<RunSummary> RunTestMethodAsync(
        ITestMethod testMethod,
        IReflectionMethodInfo method,
        IEnumerable<IXunitTestCase> testCases,
        object[] constructorArguments) =>
        new MainThreadTestMethodRunner(
            testMethod,
            Class,
            method,
            testCases,
            DiagnosticMessageSink,
            MessageBus,
            new ExceptionAggregator(Aggregator),
            CancellationTokenSource,
            constructorArguments).RunAsync();
}

internal sealed class MainThreadTestMethodRunner : XunitTestMethodRunner
{
    public MainThreadTestMethodRunner(
        ITestMethod testMethod,
        IReflectionTypeInfo @class,
        IReflectionMethodInfo method,
        IEnumerable<IXunitTestCase> testCases,
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource,
        object[] constructorArguments)
        : base(
            testMethod,
            @class,
            method,
            testCases,
            diagnosticMessageSink,
            messageBus,
            aggregator,
            cancellationTokenSource,
            constructorArguments)
    {
    }

    protected override Task<RunSummary> RunTestCaseAsync(IXunitTestCase testCase)
    {
        MainThreadTestFramework.EnterMainThreadBoundary();
        return base.RunTestCaseAsync(testCase);
    }
}
