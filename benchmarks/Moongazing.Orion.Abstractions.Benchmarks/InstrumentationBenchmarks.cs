using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Moongazing.Orion.Abstractions.Diagnostics;

namespace Moongazing.Orion.Abstractions.Benchmarks;

/// <summary>
/// Measures the per-measurement tag-stamping hot path in <see cref="OrionInstrumentation"/>.
/// <see cref="OrionInstrumentation.Tag"/> runs on every metric emission across every Orion
/// package, so its allocation profile (one array per call) is the load-bearing cost here.
/// Two static-tag cardinalities are covered: zero (the common single-tenant path, which
/// short-circuits to a single-element array) and three (a tenant/region/env split).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90)]
public class InstrumentationBenchmarks
{
    private readonly BenchInstrumentation noStaticTags = new();
    private readonly BenchInstrumentation withStaticTags = new();
    private readonly KeyValuePair<string, object?> extra = new("outcome", "ok");
    private readonly Dictionary<string, string> threeTags = new()
    {
        ["tenant"] = "acme",
        ["region"] = "eu-west-1",
        ["env"] = "prod",
    };

    [GlobalSetup]
    public void Setup() => withStaticTags.SetStaticTags(threeTags);

    /// <summary>Tag stamping when no static tags are configured (single-element fast path).</summary>
    [Benchmark(Baseline = true)]
    public KeyValuePair<string, object?>[] Tag_NoStaticTags() => noStaticTags.Tag(extra);

    /// <summary>Tag stamping with three static tags (array allocate + copy + append).</summary>
    [Benchmark]
    public KeyValuePair<string, object?>[] Tag_ThreeStaticTags() => withStaticTags.Tag(extra);

    /// <summary>
    /// Rebuilding the static-tag snapshot from a dictionary (the startup-time cost of
    /// <see cref="OrionInstrumentation.SetStaticTags"/>: allocate array + iterate + project).
    /// Reuses one instrumentation instance so only the snapshot build is measured.
    /// </summary>
    [Benchmark]
    public KeyValuePair<string, object?>[] SetStaticTags_Three()
    {
        withStaticTags.SetStaticTags(threeTags);
        return withStaticTags.StaticTags;
    }

    private sealed class BenchInstrumentation() : OrionInstrumentation("Moongazing.Orion.Benchmarks", "0.1.0");
}
