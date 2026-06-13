namespace Moongazing.Orion.Abstractions.Time;

/// <summary>
/// The production <see cref="IOrionClock"/>, forwarding to a <see cref="TimeProvider"/>
/// (defaulting to <see cref="TimeProvider.System"/>). Register as a singleton.
/// </summary>
public sealed class SystemOrionClock : IOrionClock
{
    private readonly TimeProvider timeProvider;

    /// <summary>Create a clock over <see cref="TimeProvider.System"/>.</summary>
    public SystemOrionClock()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Create a clock over an explicit <see cref="TimeProvider"/>.</summary>
    /// <param name="timeProvider">The backing time provider.</param>
    public SystemOrionClock(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    /// <inheritdoc />
    public long GetTimestamp() => timeProvider.GetTimestamp();

    /// <inheritdoc />
    public TimeSpan GetElapsedTime(long startingTimestamp) => timeProvider.GetElapsedTime(startingTimestamp);
}
