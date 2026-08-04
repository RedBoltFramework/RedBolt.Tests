namespace RedBolt.Tests;

using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Math;

[CollectionDefinition(nameof(CoreCoverageGapCollection), DisableParallelization = true)]
public sealed class CoreCoverageGapCollection
{
}

[Collection(nameof(CoreCoverageGapCollection))]
public sealed class CoreCoverageGapTests
{
    private static readonly MethodInfo SetMainThreadMethod = typeof(WorkQueue).GetMethod(
        "SetMainThread", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void Command_line_parser_covers_registration_and_parse_boundaries()
    {
        var parser = new CommandLineParser(" ");

        Assert.Equal("application", parser.ApplicationName);
        Assert.Throws<ArgumentNullException>(() => parser.AddFlag(null!, "flag", static () => { }));
        Assert.Throws<ArgumentException>(() => parser.AddFlag([string.Empty, "  "], "flag", static () => { }));
        Assert.Throws<ArgumentNullException>(() => parser.AddFlag(["--null-action"], "flag", null!));
        Assert.Throws<ArgumentNullException>(() => parser.AddOption(null!, "value", "option", static _ => { }));
        Assert.Throws<ArgumentException>(() => parser.AddOption(["--blank-value"], " ", "option", static _ => { }));
        Assert.Throws<ArgumentNullException>(() => parser.AddOption(["--null-action"], "value", "option", null!));

        var flagApplied = false;
        var optionValue = string.Empty;
        parser.AddFlag(["--flag", "-f", "--flag"], "flag", () => flagApplied = true);
        parser.AddOption(["--name", "-n"], "value", "option", value => optionValue = value);

        Assert.Throws<InvalidOperationException>(() => parser.AddFlag(["--flag"], "duplicate", static () => { }));
        Assert.True(parser.Parse([]).IsSuccess);

        var parsed = parser.Parse(["-f", "-n", "separate"]);
        Assert.True(parsed.IsSuccess);
        Assert.True(flagApplied);
        Assert.Equal("separate", optionValue);

        Assert.True(parser.Parse(["-h"]).IsHelpRequested);
        Assert.Equal("Unexpected positional argument 'scene'.", parser.Parse(["scene"]).ErrorMessage);
        Assert.Equal("Unknown option '--missing'.", parser.Parse(["--missing"]).ErrorMessage);
        Assert.Throws<ArgumentNullException>(() => parser.Parse(null!));

        foreach (var exceptionFactory in new Func<Exception>[]
        {
            static () => new ArgumentException("argument failure"),
            static () => new FormatException("format failure"),
            static () => new InvalidOperationException("operation failure")
        })
        {
            var throwingParser = new CommandLineParser("test");
            throwingParser.AddOption(["--throw"], "value", "throws", _ => throw exceptionFactory());

            var result = throwingParser.Parse(["--throw", "value"]);

            Assert.False(result.IsSuccess);
            Assert.Equal(exceptionFactory().Message, result.ErrorMessage);
        }
    }

    [Fact]
    public void Process_utils_covers_argument_edges_console_sinks_and_path_normalization()
    {
        Assert.Contains(ProcessUtils.DynLibSuffix, [".so", ".dll", ".dylib"]);

        Assert.Empty(ProcessUtils.ParseArguments(new[] { "program" }));
        Assert.Equal(
            ["two words", "plain"],
            ProcessUtils.ParseArguments("\"two words\"   plain", skipFirstArgument: false));
        Assert.Equal(["unclosed value"], ProcessUtils.ParseArguments("program \"unclosed value", skipFirstArgument: true));
        Assert.Throws<ArgumentNullException>(() => ProcessUtils.ParseArguments((string)null!));
        Assert.Throws<ArgumentNullException>(() => ProcessUtils.ParseArguments((IReadOnlyList<string>)null!));

        ProcessUtils.SetMiniDumpDir("/tmp/redbolt-minidumps" + Path.DirectorySeparatorChar);
        Assert.Equal("/tmp/redbolt-minidumps" + Path.DirectorySeparatorChar, ProcessUtils.GetMiniDumpDir());
        Assert.Throws<ArgumentNullException>(() => ProcessUtils.SetMiniDumpDir(null!));
        ProcessUtils.SetMiniDumpDir(string.Empty);
        Assert.Equal(string.Empty, ProcessUtils.GetMiniDumpDir());

        var originalOutput = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            ProcessUtils.PrintUnicode("out");
            ProcessUtils.PrintUnicodeLine("put");
            ProcessUtils.PrintLine("line");
            ProcessUtils.PrintUnicode("err", error: true);
            ProcessUtils.PrintUnicodeLine("or", error: true);
            ProcessUtils.PrintLine("!", error: true);
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }

        Assert.Equal("output\nline\n", output.ToString());
        Assert.Equal("error\n!\n", error.ToString());
    }

    [Fact]
    public void Spline_covers_empty_singleton_mutation_and_invalid_mode_paths()
    {
        using var empty = new Spline();
        Assert.Equal(InterpolationMode.BezierCurve, empty.GetInterpolationMode());
        Assert.Empty(empty.GetKnots());
        using var emptyPoint = empty.GetPoint(0.5f);
        Assert.Equal(VariantType.None, emptyPoint.Type);

        using var singletonKnots = new VariantVector { new(7.0f) };
        using var singleton = new Spline(singletonKnots);
        using var singletonPoint = singleton.GetPoint(0.5f);
        using var singletonKnot = singleton.GetKnot(0);
        Assert.Equal(7.0f, singletonPoint.GetFloat());
        Assert.Equal(7.0f, singletonKnot.GetFloat());

        using var mutableKnots = new VariantVector { new(1.0f), new(2.0f) };
        using var mutable = new Spline(mutableKnots);
        mutable.SetInterpolationMode(InterpolationMode.LinearCurve);
        mutable.AddKnot(new Variant(3.0f), 100);
        mutable.SetKnot(new Variant(4.0f), 0);
        mutable.SetKnot(new Variant(5), 0);
        mutable.SetKnot(new Variant(6.0f), 100);
        mutable.AddKnot(new Variant(7));
        Assert.Equal(3, mutable.GetKnots().Count);

        using var emptyMutation = new Spline();
        emptyMutation.SetKnot(new Variant(11.0f), -1);
        Assert.Single(emptyMutation.GetKnots());

        using var replacement = new VariantVector { new(8.0f), new(9.0f) };
        mutable.SetKnots(replacement);
        mutable.RemoveKnot(1);
        mutable.RemoveKnot();
        Assert.Empty(mutable.GetKnots());
        mutable.Clear();

        using var unsupported = CreateSpline((InterpolationMode)999, new Variant(1.0f), new Variant(2.0f));
        using var unsupportedPoint = unsupported.GetPoint(0.5f);
        Assert.Equal(VariantType.None, unsupportedPoint.Type);
    }

    [Fact]
    public void Spline_covers_supported_bezier_linear_and_catmull_types()
    {
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Float, new Variant(0.0f), new Variant(10.0f));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Vector2,
            new Variant(new Vector2f(0.0f, 0.0f)), new Variant(new Vector2f(10.0f, 10.0f)));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Vector3,
            new Variant(new Vector3f(0.0f, 0.0f, 0.0f)), new Variant(new Vector3f(10.0f, 10.0f, 10.0f)));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Vector4,
            new Variant(new Vector4f(0.0f, 0.0f, 0.0f, 0.0f)), new Variant(new Vector4f(10.0f, 10.0f, 10.0f, 10.0f)));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Color,
            new Variant(Color.Black), new Variant(Color.White));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Double, new Variant(0.0), new Variant(10.0));

        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.Vector4,
            new Variant(new Vector4f(0.0f, 0.0f, 0.0f, 0.0f)),
            new Variant(new Vector4f(1.0f, 1.0f, 1.0f, 1.0f)),
            new Variant(new Vector4f(2.0f, 2.0f, 2.0f, 2.0f)),
            new Variant(new Vector4f(3.0f, 3.0f, 3.0f, 3.0f)));
        AssertSplinePointType(InterpolationMode.BezierCurve, VariantType.None, new Variant(0), new Variant(1));

        AssertSplinePointType(InterpolationMode.LinearCurve, VariantType.Float, new Variant(0.0f), new Variant(10.0f));
        AssertSplinePointType(InterpolationMode.LinearCurve, VariantType.Vector2,
            new Variant(new Vector2f(0.0f, 0.0f)), new Variant(new Vector2f(10.0f, 10.0f)));
        AssertSplinePointType(InterpolationMode.LinearCurve, VariantType.Vector3,
            new Variant(new Vector3f(0.0f, 0.0f, 0.0f)), new Variant(new Vector3f(10.0f, 10.0f, 10.0f)));
        AssertSplinePointType(InterpolationMode.LinearCurve, VariantType.Vector4,
            new Variant(new Vector4f(0.0f, 0.0f, 0.0f, 0.0f)), new Variant(new Vector4f(10.0f, 10.0f, 10.0f, 10.0f)));
        AssertSplinePointType(InterpolationMode.LinearCurve, VariantType.Color,
            new Variant(Color.Black), new Variant(Color.White));
        AssertSplinePointType(InterpolationMode.LinearCurve, VariantType.Double, new Variant(0.0), new Variant(10.0));
        AssertSplinePointType(InterpolationMode.LinearCurve, VariantType.None, new Variant(0), new Variant(1));

        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Float,
            new Variant(0.0f), new Variant(10.0f), new Variant(20.0f), new Variant(30.0f));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Vector2,
            new Variant(new Vector2f(0.0f, 0.0f)), new Variant(new Vector2f(10.0f, 10.0f)),
            new Variant(new Vector2f(20.0f, 20.0f)), new Variant(new Vector2f(30.0f, 30.0f)));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Vector3,
            new Variant(new Vector3f(0.0f, 0.0f, 0.0f)), new Variant(new Vector3f(10.0f, 10.0f, 10.0f)),
            new Variant(new Vector3f(20.0f, 20.0f, 20.0f)), new Variant(new Vector3f(30.0f, 30.0f, 30.0f)));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Vector4,
            new Variant(new Vector4f(0.0f, 0.0f, 0.0f, 0.0f)), new Variant(new Vector4f(10.0f, 10.0f, 10.0f, 10.0f)),
            new Variant(new Vector4f(20.0f, 20.0f, 20.0f, 20.0f)), new Variant(new Vector4f(30.0f, 30.0f, 30.0f, 30.0f)));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Color,
            new Variant(Color.Black), new Variant(new Color(0.25f, 0.25f, 0.25f)),
            new Variant(new Color(0.75f, 0.75f, 0.75f)), new Variant(Color.White));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.Double,
            new Variant(0.0), new Variant(10.0), new Variant(20.0), new Variant(30.0));
        AssertSplinePointType(InterpolationMode.CatmullRomCurve, VariantType.None,
            new Variant(0), new Variant(10), new Variant(20), new Variant(30));

        using var endpointSpline = CreateSpline(InterpolationMode.CatmullRomCurve,
            new Variant(0.0f), new Variant(10.0f), new Variant(20.0f), new Variant(30.0f));
        using var endpoint = endpointSpline.GetPoint(1.0f);
        Assert.Equal(20.0f, endpoint.GetFloat());

        AssertSplinePointType(InterpolationMode.CatmullRomFullCurve, VariantType.Float,
            new Variant(0.0f), new Variant(10.0f), new Variant(20.0f));
        AssertSplinePointType(InterpolationMode.CatmullRomFullCurve, VariantType.Float,
            new Variant(0.0f), new Variant(10.0f), new Variant(0.0f));
    }

    [Fact]
    public void Variant_curve_covers_spline_types_tangents_and_loop_sampling()
    {
        AssertTensionCurve(VariantType.Float, static () => new Variant(0.0f), static () => new Variant(10.0f), static () => new Variant(20.0f));
        AssertTensionCurve(VariantType.Vector2,
            static () => new Variant(new Vector2f(0.0f, 0.0f)), static () => new Variant(new Vector2f(10.0f, 10.0f)),
            static () => new Variant(new Vector2f(20.0f, 20.0f)));
        AssertTensionCurve(VariantType.Vector3,
            static () => new Variant(new Vector3f(0.0f, 0.0f, 0.0f)), static () => new Variant(new Vector3f(10.0f, 10.0f, 10.0f)),
            static () => new Variant(new Vector3f(20.0f, 20.0f, 20.0f)));
        AssertTensionCurve(VariantType.Vector4,
            static () => new Variant(new Vector4f(0.0f, 0.0f, 0.0f, 0.0f)), static () => new Variant(new Vector4f(10.0f, 10.0f, 10.0f, 10.0f)),
            static () => new Variant(new Vector4f(20.0f, 20.0f, 20.0f, 20.0f)));
        AssertTensionCurve(VariantType.Quaternion,
            static () => new Variant(Quaternion.IDENTITY), static () => new Variant(new Quaternion(30.0f, Vector3f.Up)),
            static () => new Variant(new Quaternion(60.0f, Vector3f.Up)));
        AssertTensionCurve(VariantType.Color,
            static () => new Variant(Color.Black), static () => new Variant(new Color(0.5f, 0.5f, 0.5f)), static () => new Variant(Color.White));
        AssertTensionCurve(VariantType.Double, static () => new Variant(0.0), static () => new Variant(10.0), static () => new Variant(20.0));

        foreach (var factory in new Func<Variant>[]
        {
            static () => new Variant(1L),
            static () => new Variant(new IntRect(0, 0, 1, 1)),
            static () => new Variant(new Vector2i(1, 1)),
            static () => new Variant(new Vector3i(1, 1, 1))
        })
        {
            using var curve = CreateCurve(KeyFrameInterpolation.TensionSpline, factory, factory);
            curve.Commit();
            Assert.Equal(KeyFrameInterpolation.Linear, curve.Interpolation);
        }

        using var unsupported = CreateCurve(KeyFrameInterpolation.TangentSpline,
            static () => new Variant("a"), static () => new Variant("b"));
        unsupported.Commit();
        Assert.Equal(VariantType.String, unsupported.Type);
        Assert.Equal(KeyFrameInterpolation.None, unsupported.Interpolation);

        using var cyclic = CreateCurve(KeyFrameInterpolation.TensionSpline,
            static () => new Variant(0.0f), static () => new Variant(10.0f), static () => new Variant(0.0f));
        cyclic.Commit();
        Assert.Equal(cyclic.InTangents[0], cyclic.InTangents[^1]);
        using var cyclicSample = cyclic.Sample(0.5f);
        Assert.Equal(VariantType.Float, cyclicSample.Type);

        using var tangent = CreateCurve(KeyFrameInterpolation.TangentSpline,
            static () => new Variant(0.0f), static () => new Variant(10.0f));
        tangent.Commit();
        tangent.InTangents.Add(new Variant(2.0f));
        tangent.InTangents.Add(new Variant(2.0f));
        tangent.OutTangents.Add(new Variant(2.0f));
        tangent.OutTangents.Add(new Variant(2.0f));
        using var tangentSample = tangent.Sample(0.5f);
        Assert.Equal(VariantType.Float, tangentSample.Type);
    }

    [Fact]
    public void Variant_curve_covers_identity_hash_and_loop_boundaries()
    {
        using var empty = new VariantCurve();
        Assert.Equal(VariantType.None, empty.GetType());

        using var curve = CreateCurve(KeyFrameInterpolation.Linear,
            static () => new Variant(0.0f), static () => new Variant(10.0f), static () => new Variant(20.0f));
        curve.Name = "Loop";
        curve.SplineTension = 0.25f;
        curve.Commit();

        uint frameIndex = 2;
        using var wrapped = curve.Sample(2.5f, 3.0f, isLooped: true, ref frameIndex);
        Assert.Equal(10.0f, wrapped.GetFloat());

        using var first = curve.Sample(0.0f);
        Assert.Equal(0.0f, first.GetFloat());
        using var clone = curve.Clone();
        Assert.True(curve.Equals(clone));
        Assert.True(curve.Equals((object)clone));
        Assert.Equal(curve.ToHash(), clone.ToHash());
        Assert.True(curve == clone);
        Assert.False(curve != clone);
        Assert.False(curve.Equals(null));
        Assert.False(curve == null);
        Assert.True(curve != null);

        var point = new VariantCurvePoint { Time = 0.5f, Value = new Variant(3.0f) };
        var pointClone = point.Clone();
        Assert.True(point.Equals(pointClone));
        Assert.True(point.Equals((object)pointClone));
        Assert.Equal(point.ToHash(), pointClone.ToHash());
        Assert.False(point != pointClone);
        point.Dispose();
        pointClone.Dispose();
    }

    [Fact]
    public void Work_queue_covers_single_thread_public_paths_and_parallel_helpers()
    {
        using var queue = CreateWorkQueue();

        Assert.Equal(1, queue.GetNumProcessingThreads());
        Assert.False(queue.IsMultithreaded());
        queue.SetNonThreadedWorkMs(0);
        Assert.Equal(1, queue.GetNonThreadedWorkMs());

        var immediate = false;
        queue.PostTask(() => immediate = true, TaskPriority.Immediate);
        Assert.True(immediate);

        var fallback = false;
        queue.PostTask(() => fallback = true, TaskPriority.High);
        Assert.False(fallback);
        queue.Update();
        Assert.True(fallback);

        queue.PostTask(() => Thread.Sleep(5), TaskPriority.Low);
        queue.PostTask(() => { }, TaskPriority.Low);
        queue.Update();

        var threadTask = false;
        queue.PostTaskForThread(() => threadTask = true, TaskPriority.Immediate, 0);
        Assert.True(threadTask);
        queue.PostTaskForThread(() => threadTask = false, TaskPriority.High, 0);
        queue.CompleteAll();
        Assert.False(threadTask);

        var mainTask = false;
        var external = new Thread(() => queue.PostTaskForMainThread(() => mainTask = true));
        external.Start();
        external.Join();
        Assert.False(mainTask);
        queue.CompleteAll();
        Assert.True(mainTask);

        var externalPost = false;
        external = new Thread(() => queue.PostTask(() => externalPost = true, TaskPriority.Immediate));
        external.Start();
        external.Join();
        queue.CompleteAll();
        Assert.True(externalPost);

#pragma warning disable CS0618
        var legacyCalls = 0;
        queue.AddWorkItem(_ => ++legacyCalls, priority: 0);
        queue.AddWorkItem(_ => ++legacyCalls, priority: uint.MaxValue);
        queue.AddWorkItem(_ => ++legacyCalls, priority: 2);
        queue.AddWorkItem(new WorkItem());
        queue.Complete(uint.MaxValue);
        queue.Complete(0);
#pragma warning restore CS0618
        Assert.Equal(3, legacyCalls);

        var delayed = false;
        queue.PostDelayedTaskForMainThread(() => delayed = true);
        Assert.False(delayed);
        queue.CompleteAll();
        Assert.True(delayed);

        var ranges = new List<(int Begin, int End)>();
        WorkQueue.ForEachParallel(queue, 4, 0, (begin, end) => ranges.Add((begin, end)));
        WorkQueue.ForEachParallel(queue, 2, 5, (begin, end) => ranges.Add((begin, end)));
        Assert.Equal([(0, 2), (2, 4), (4, 5)], ranges);

        var nested = false;
        WorkQueue.ForEachParallel(queue, 1, new[] { 1, 2 }, (_, _) =>
        {
            if (!nested)
            {
                nested = true;
                WorkQueue.ForEachParallel(queue, 1, new[] { 3 }, (_, _) => { });
            }
        });
        Assert.True(nested);

        using var multiVector = new MultiVector<int>();
        multiVector.Resize(2);
        var first = 10;
        var second = 20;
        var third = 30;
        multiVector.PushBack(0, in first);
        multiVector.PushBack(0, in second);
        multiVector.PushBack(1, in third);
        var collected = new List<int>();
        WorkQueue.ForEachParallel(queue, 1, multiVector, (_, value) => collected.Add(value));
        Assert.Equal([10, 20, 30], collected);

        using var vector = new WorkQueueVector<int>();
        vector.Clear();
        ref var emplaced = ref vector.Emplace();
        emplaced = 4;
        ref var factoryEmplaced = ref vector.Emplace(static () => 5);
        Assert.Equal(5, factoryEmplaced);
        Assert.Equal(2, vector.Size());

        Assert.Throws<ArgumentNullException>(() => WorkQueue.ForEachParallel(null!, 1, 1, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => WorkQueue.ForEachParallel(queue, 1, 1, null!));
        Assert.Throws<ArgumentNullException>(() => WorkQueue.ForEachParallel<int>(queue, 1, (IReadOnlyList<int>)null!, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => WorkQueue.ForEachParallel<int>(queue, 1, [1], null!));
        Assert.Throws<ArgumentNullException>(() => WorkQueue.ForEachParallel<int>(queue, 1, (MultiVector<int>)null!, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => WorkQueue.ForEachParallel<int>(queue, 1, multiVector, null!));
    }

    [Fact]
    public void Work_queue_covers_multithreaded_dispatch_and_completion()
    {
        using var queue = CreateWorkQueue(2);

        Assert.True(queue.IsMultithreaded());
        Assert.Equal(3, queue.GetNumProcessingThreads());

        var threadIndices = new ConcurrentBag<int>();
        queue.PostTask((int threadIndex, WorkQueue _) => threadIndices.Add(threadIndex), TaskPriority.Immediate);
        Assert.True(queue.GetNumIncomplete() > 0);
        queue.CompleteImmediateForThisThread();
        Assert.Single(threadIndices);

        queue.PostTask((int threadIndex, WorkQueue _) => threadIndices.Add(threadIndex), TaskPriority.High);
        queue.PostTaskForThread((int threadIndex, WorkQueue _) => threadIndices.Add(threadIndex), TaskPriority.Immediate, 1);
        queue.PostTaskForMainThread((int threadIndex, WorkQueue _) => threadIndices.Add(threadIndex), TaskPriority.Immediate);
        queue.CompleteAll();
        Assert.True(threadIndices.Count >= 4);
        Assert.True(queue.IsCompleted());

        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        var disposingQueue = CreateWorkQueue(1);
        disposingQueue.PostTaskForThread((_, _) =>
        {
            started.Set();
            release.Wait();
        }, TaskPriority.Immediate, 1);
        Assert.True(started.Wait(TimeSpan.FromSeconds(1)));
        var disposeTask = Task.Run(disposingQueue.Dispose);
        Thread.Sleep(10);
        Assert.False(disposeTask.IsCompleted);
        release.Set();
        Assert.True(disposeTask.Wait(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Work_queue_routes_external_thread_targeted_tasks_through_main_thread()
    {
        using var queue = CreateWorkQueue();
        var queueField = typeof(WorkQueue).GetField("_workQueue", BindingFlags.Static | BindingFlags.NonPublic)!;
        var threadCountField = typeof(WorkQueue).GetField("_threadIndexCount", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previousQueue = queueField.GetValue(null);
        var previousThreadCount = threadCountField.GetValue(null);
        try
        {
            queueField.SetValue(null, queue);
            threadCountField.SetValue(null, 1);

            var called = false;
            var external = new Thread(() => queue.PostTaskForThread(
                () => called = true, TaskPriority.Immediate, 0));
            external.Start();
            external.Join();

            Assert.False(called);
            queue.CompleteAll();
            Assert.True(called);
        }
        finally
        {
            queueField.SetValue(null, previousQueue);
            threadCountField.SetValue(null, previousThreadCount);
        }
    }

    [Fact]
    public void Time_covers_frame_properties_timer_period_and_timestamp_formats()
    {
        using var time = new Time();

        Assert.Equal(0u, time.FrameNumber);
        Assert.Equal(0.0f, time.TimeStep);
        time.SetTimerPeriod(42);
        Assert.Equal(42u, time.TimerPeriod);
        Assert.Equal(42u, time.GetTimerPeriod());

        time.BeginFrame(0.25f);
        Assert.Equal(1u, time.GetFrameNumber());
        Assert.Equal(0.25f, time.GetTimeStep());
        Assert.Equal(4.0f, time.GetFramesPerSecond());
        Assert.True(time.ElapsedTime >= 0.0f);
        Assert.True(time.IsFrameInProgress);
        time.EndFrame();
        Assert.False(time.IsFrameInProgress);

        Assert.Equal("1970", Time.GetTimeStamp(0, "yyyy"));
        Time.Sleep(0);
    }

    private static WorkQueue CreateWorkQueue(int numThreads = 0)
    {
        var queue = new WorkQueue();
        SetMainThreadMethod.Invoke(null, null);
        queue.Initialize(numThreads);
        return queue;
    }

    private static Spline CreateSpline(InterpolationMode mode, params Variant[] values)
    {
        using var knots = new VariantVector();
        foreach (var value in values)
        {
            knots.Add(value);
            value.Dispose();
        }

        return new Spline(knots, mode);
    }

    private static void AssertSplinePointType(InterpolationMode mode, VariantType expectedType, params Variant[] values)
    {
        using var spline = CreateSpline(mode, values);
        using var point = spline.GetPoint(0.5f);
        Assert.Equal(expectedType, point.Type);
    }

    private static VariantCurve CreateCurve(KeyFrameInterpolation interpolation, params Func<Variant>[] valueFactories)
    {
        var curve = new VariantCurve { Interpolation = interpolation };
        for (var index = 0; index < valueFactories.Length; ++index)
        {
            using var value = valueFactories[index]();
            curve.AddKeyFrame(new VariantCurvePoint { Time = index, Value = value });
        }

        return curve;
    }

    private static void AssertTensionCurve(VariantType expectedType, params Func<Variant>[] valueFactories)
    {
        using var curve = CreateCurve(KeyFrameInterpolation.TensionSpline, valueFactories);
        curve.Commit();
        Assert.Equal(expectedType, curve.Type);
        Assert.Equal(curve.KeyFrames.Count, curve.InTangents.Count);
        Assert.Equal(curve.KeyFrames.Count, curve.OutTangents.Count);
        using var sample = curve.Sample(0.5f);
        Assert.Equal(expectedType, sample.Type);
    }
}
