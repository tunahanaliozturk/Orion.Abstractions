namespace Moongazing.Orion.Abstractions.Results;

/// <summary>
/// The canonical <see cref="OrionError.Code"/> values. A package should reach for one of these
/// before defining its own, so a consumer can handle "this failed because it conflicted"
/// identically whether it came from <c>OrionLock</c>, <c>OrionOnce</c>, or <c>OrionPatch</c> -
/// and so the web tier can map an error to a status code without a per-package lookup table.
/// </summary>
/// <remarks>
/// The set is intentionally small and closed-ish, and mirrors the widely-understood canonical
/// codes (gRPC / Google API design guide) rather than inventing a fourth vocabulary. A package
/// with a genuinely package-specific condition prefixes its own code with its short component
/// name, e.g. <c>lock_lease_lost</c>.
/// </remarks>
public static class OrionErrorCodes
{
    /// <summary>An argument or configuration value was rejected before any work was attempted.</summary>
    public const string InvalidArgument = "invalid_argument";

    /// <summary>The addressed entity does not exist.</summary>
    public const string NotFound = "not_found";

    /// <summary>The entity already exists, or the operation lost a race for it.</summary>
    public const string Conflict = "conflict";

    /// <summary>The operation could not run because the entity was in the wrong state.</summary>
    public const string FailedPrecondition = "failed_precondition";

    /// <summary>The caller could not be identified.</summary>
    public const string Unauthenticated = "unauthenticated";

    /// <summary>The caller was identified but is not allowed to do this.</summary>
    public const string PermissionDenied = "permission_denied";

    /// <summary>The caller exceeded a quota or rate limit.</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>The operation exceeded its deadline.</summary>
    public const string Timeout = "timeout";

    /// <summary>The operation was cancelled cooperatively.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>A dependency was unreachable or unhealthy; retrying may succeed.</summary>
    public const string Unavailable = "unavailable";

    /// <summary>An unexpected fault. The fallback when nothing more specific fits.</summary>
    public const string Internal = "internal";
}
