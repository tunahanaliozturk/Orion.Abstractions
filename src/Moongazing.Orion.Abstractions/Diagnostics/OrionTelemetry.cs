namespace Moongazing.Orion.Abstractions.Diagnostics;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// The frozen OpenTelemetry naming surface for the Orion family. Every
/// <see cref="ActivitySource"/> name, <see cref="Meter"/> name, metric name, and tag key a
/// package emits comes from here - so a dashboard written against one package's signals
/// generalises to all of them, and no package invents a magic string.
/// </summary>
/// <remarks>
/// <para>
/// Two naming schemes, deliberately: instrumentation <em>scopes</em> (ActivitySource / Meter)
/// use the assembly-style <c>Moongazing.OrionLock</c> so they match the shipping package
/// names an operator enables in their OTel config, while <em>metrics</em> and <em>tags</em>
/// use the lowercase dotted <c>orion.lock.acquire.duration</c> form the OpenTelemetry
/// semantic conventions mandate.
/// </para>
/// </remarks>
public static class OrionTelemetry
{
    /// <summary>The prefix on every Orion instrumentation scope name.</summary>
    public const string ScopePrefix = "Moongazing.";

    /// <summary>The prefix on every Orion metric name and tag key.</summary>
    public const string MetricPrefix = "orion.";

    /// <summary>
    /// The instrumentation scope name (used for both the <see cref="ActivitySource"/> and the
    /// <see cref="Meter"/>) for a package, e.g. <c>ScopeName("OrionLock")</c> is
    /// <c>Moongazing.OrionLock</c>.
    /// </summary>
    /// <param name="packageName">The package's assembly-style name, e.g. <c>OrionLock</c>.</param>
    public static string ScopeName(string packageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        return packageName.StartsWith(ScopePrefix, StringComparison.Ordinal)
            ? packageName
            : ScopePrefix + packageName;
    }

    /// <summary>
    /// A metric name in the family's convention: <c>orion.{component}.{instrument}</c>, e.g.
    /// <c>MetricName("lock", "acquire.duration")</c> is <c>orion.lock.acquire.duration</c>.
    /// </summary>
    /// <param name="component">
    /// The lowercase short component name - the package name without the <c>Orion</c> prefix,
    /// e.g. <c>lock</c> for <c>OrionLock</c>.
    /// </param>
    /// <param name="instrument">
    /// The lowercase dotted instrument name, e.g. <c>acquire.duration</c>.
    /// </param>
    public static string MetricName(string component, string instrument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);
        ArgumentException.ThrowIfNullOrWhiteSpace(instrument);
        return string.Concat(MetricPrefix, component, ".", instrument);
    }

    /// <summary>
    /// The frozen tag keys. A package may add its own domain tags, but anything covered here
    /// MUST use these keys so cross-package dashboards can group and filter uniformly.
    /// </summary>
    public static class Tags
    {
        /// <summary>Per-instance scope id; see <see cref="OrionInstrumentation.InstanceScopeId"/>.</summary>
        public const string Instance = MetricPrefix + "instance";

        /// <summary>The package emitting the signal, e.g. <c>OrionLock</c>.</summary>
        public const string Package = MetricPrefix + "package";

        /// <summary>The tenant a multi-tenant deployment attributes the signal to.</summary>
        public const string Tenant = MetricPrefix + "tenant";

        /// <summary>The deployment region.</summary>
        public const string Region = MetricPrefix + "region";

        /// <summary>The logical operation, e.g. <c>acquire</c>, <c>dispatch</c>, <c>renew</c>.</summary>
        public const string Operation = MetricPrefix + "operation";

        /// <summary>How the operation ended; one of <see cref="Outcomes"/>.</summary>
        public const string Outcome = MetricPrefix + "outcome";

        /// <summary>The <see cref="Results.OrionError.Code"/> when the outcome is a failure.</summary>
        public const string ErrorCode = MetricPrefix + "error.code";

        /// <summary>The 1-based attempt number on a retried operation.</summary>
        public const string Attempt = MetricPrefix + "attempt";
    }

    /// <summary>
    /// The frozen values for the <see cref="Tags.Outcome"/> tag. Bounded on purpose: outcome
    /// is a low-cardinality dimension, so failures are distinguished by
    /// <see cref="Tags.ErrorCode"/> rather than by inventing new outcomes.
    /// </summary>
    public static class Outcomes
    {
        /// <summary>The operation completed as intended.</summary>
        public const string Success = "success";

        /// <summary>The operation completed but did not achieve its intent.</summary>
        public const string Failure = "failure";

        /// <summary>The operation was cancelled cooperatively.</summary>
        public const string Cancelled = "cancelled";

        /// <summary>The operation exceeded its deadline.</summary>
        public const string Timeout = "timeout";
    }
}
