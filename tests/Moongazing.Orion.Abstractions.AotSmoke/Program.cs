// NativeAOT smoke test. Publishing this with PublishAot=true must produce zero trim/AOT
// warnings, and running the binary must exit 0 - that pair is the spine's AOT exit criterion.
// Every assertion here is a runtime check, not a test-framework assertion, because the point is
// to prove the code paths survive trimming in a real native binary.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moongazing.Orion.Abstractions;
using Moongazing.Orion.Abstractions.Configuration;
using Moongazing.Orion.Abstractions.Diagnostics;
using Moongazing.Orion.Abstractions.Observers;
using Moongazing.Orion.Abstractions.Results;
using Moongazing.Orion.Abstractions.Time;

var services = new ServiceCollection();
services.AddOrionAbstractions();
services.AddOrionOptions<SmokeOptions>(o => o.LeaseDuration = TimeSpan.FromSeconds(30));

using var provider = services.BuildServiceProvider();

// The clock seam resolves and both time surfaces agree.
var clock = provider.GetRequiredService<IOrionClock>();
var viaProperty = clock.UtcNow;
var viaMethod = clock.GetUtcNow();
Check(viaMethod >= viaProperty, "clock surfaces disagree");
var started = clock.GetTimestamp();
Check(clock.GetElapsedTime(started) >= TimeSpan.Zero, "elapsed time went backwards");

// Options bind and validate without reflection-based binding.
var options = provider.GetRequiredService<IOptions<SmokeOptions>>().Value;
Check(options.LeaseDuration == TimeSpan.FromSeconds(30), "options were not configured");

var invalid = new ServiceCollection();
invalid.AddOrionOptions<SmokeOptions>(o => o.LeaseDuration = TimeSpan.Zero);
using var invalidProvider = invalid.BuildServiceProvider();
try
{
    _ = invalidProvider.GetRequiredService<IOptions<SmokeOptions>>().Value;
    Check(false, "invalid options were accepted");
}
catch (OptionsValidationException)
{
    // Expected: validation runs under AOT.
}

// Instrumentation constructs its ActivitySource and Meter, scoped and unscoped.
using var instrumentation = new SmokeInstrumentation();
instrumentation.SetStaticTags(new Dictionary<string, string> { ["orion.tenant"] = "smoke" });
Check(instrumentation.Tag(new("k", "v")).Length == 2, "static tag stamping failed");
Check(OrionTelemetry.ScopeName("OrionLock") == "Moongazing.OrionLock", "scope naming failed");
Check(
    OrionTelemetry.MetricName("lock", "acquire.duration") == "orion.lock.acquire.duration",
    "metric naming failed");

// The observer guard swallows a faulting observer.
var faulted = false;
SafeObserverInvoker.Invoke(new object(), _ => throw new InvalidOperationException(), _ => faulted = true);
Check(faulted, "observer fault was not reported");

// The error vocabulary constructs and renders.
var error = new OrionError(OrionErrorCodes.Conflict, "held", "orders:42");
Check(error.ToString() == "conflict (orders:42): held", "error rendering failed");

Console.WriteLine("Orion.Abstractions AOT smoke test passed.");
return 0;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        Console.Error.WriteLine($"AOT smoke test failed: {message}");
        Environment.Exit(1);
    }
}

internal sealed class SmokeOptions : OrionOptions
{
    public TimeSpan LeaseDuration { get; set; }

    public override void Validate(OrionOptionsValidationContext context)
    {
        base.Validate(context);
        context.RequirePositive(LeaseDuration, nameof(LeaseDuration));
    }
}

internal sealed class SmokeInstrumentation()
    : OrionInstrumentation(OrionTelemetry.ScopeName("OrionSmoke"), "1.0.0", instanceScopeId: "smoke-1");
