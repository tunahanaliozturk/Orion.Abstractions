namespace Moongazing.Orion.Abstractions.Tests;

using System;
using System.Threading;

using Moongazing.Orion.Abstractions.Testing;
using Moongazing.Orion.Abstractions.Time;

using Xunit;

/// <summary>
/// Coverage for <see cref="OrionDeadline"/>: it expires deterministically as a
/// <see cref="FrozenOrionClock"/> is advanced, reports remaining budget clamped to zero, treats
/// <see cref="OrionDeadline.Never"/> / infinite budgets as no-deadline, and validates its inputs.
/// </summary>
public sealed class OrionDeadlineTests
{
    [Fact]
    public void A_fresh_deadline_is_not_expired_and_reports_the_full_budget()
    {
        var clock = new FrozenOrionClock();
        var deadline = OrionDeadline.After(clock, TimeSpan.FromSeconds(30));

        Assert.True(deadline.HasBudget);
        Assert.False(deadline.IsExpired(clock));
        Assert.Equal(TimeSpan.FromSeconds(30), deadline.Remaining(clock));
    }

    [Fact]
    public void Advancing_the_clock_consumes_the_budget_then_expires()
    {
        var clock = new FrozenOrionClock();
        var deadline = OrionDeadline.After(clock, TimeSpan.FromSeconds(30));

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.False(deadline.IsExpired(clock));
        Assert.Equal(TimeSpan.FromSeconds(20), deadline.Remaining(clock));

        clock.Advance(TimeSpan.FromSeconds(20)); // now at the budget
        Assert.True(deadline.IsExpired(clock));
        Assert.Equal(TimeSpan.Zero, deadline.Remaining(clock));
    }

    [Fact]
    public void Remaining_is_clamped_to_zero_past_the_deadline()
    {
        var clock = new FrozenOrionClock();
        var deadline = OrionDeadline.After(clock, TimeSpan.FromSeconds(5));

        clock.Advance(TimeSpan.FromSeconds(60));

        Assert.True(deadline.IsExpired(clock));
        Assert.Equal(TimeSpan.Zero, deadline.Remaining(clock));
    }

    [Fact]
    public void A_non_positive_budget_is_already_expired()
    {
        var clock = new FrozenOrionClock();

        Assert.True(OrionDeadline.After(clock, TimeSpan.Zero).IsExpired(clock));
        Assert.True(OrionDeadline.After(clock, TimeSpan.FromSeconds(-1)).IsExpired(clock));
    }

    [Fact]
    public void Never_does_not_expire_and_reports_infinite_remaining()
    {
        var clock = new FrozenOrionClock();
        var never = OrionDeadline.Never;

        clock.Advance(TimeSpan.FromDays(3650));

        Assert.False(never.HasBudget);
        Assert.False(never.IsExpired(clock));
        Assert.Equal(Timeout.InfiniteTimeSpan, never.Remaining(clock));
    }

    [Fact]
    public void An_infinite_budget_is_Never()
    {
        var clock = new FrozenOrionClock();
        var deadline = OrionDeadline.After(clock, Timeout.InfiniteTimeSpan);

        Assert.Equal(OrionDeadline.Never, deadline);
        Assert.False(deadline.IsExpired(clock));
    }

    [Fact]
    public void The_default_value_is_Never()
    {
        var clock = new FrozenOrionClock();
        OrionDeadline defaulted = default;

        Assert.Equal(OrionDeadline.Never, defaulted);
        Assert.False(defaulted.HasBudget);
        Assert.False(defaulted.IsExpired(clock));
    }

    [Fact]
    public void After_rejects_a_null_clock()
        => Assert.Throws<ArgumentNullException>(() => OrionDeadline.After(null!, TimeSpan.FromSeconds(1)));

    [Fact]
    public void IsExpired_and_Remaining_reject_a_null_clock()
    {
        var clock = new FrozenOrionClock();
        var deadline = OrionDeadline.After(clock, TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentNullException>(() => deadline.IsExpired(null!));
        Assert.Throws<ArgumentNullException>(() => deadline.Remaining(null!));
    }

    [Fact]
    public void Equality_reflects_budget_and_origin()
    {
        var clock = new FrozenOrionClock();
        var a = OrionDeadline.After(clock, TimeSpan.FromSeconds(30));
        var b = OrionDeadline.After(clock, TimeSpan.FromSeconds(30)); // same timestamp (frozen) + budget
        var c = OrionDeadline.After(clock, TimeSpan.FromSeconds(31));

        Assert.True(a == b);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
