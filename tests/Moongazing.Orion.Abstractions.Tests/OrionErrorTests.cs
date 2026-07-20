namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Results;
using Xunit;

public sealed class OrionErrorTests
{
    [Fact]
    public void An_error_carries_its_code_message_and_target()
    {
        var error = new OrionError(OrionErrorCodes.Conflict, "The lock is held.", "orders:42");

        Assert.Equal("conflict", error.Code);
        Assert.Equal("The lock is held.", error.Message);
        Assert.Equal("orders:42", error.Target);
    }

    [Fact]
    public void The_target_is_optional()
    {
        var error = new OrionError(OrionErrorCodes.Internal, "Boom.");

        Assert.Null(error.Target);
    }

    [Theory]
    [InlineData(null, "message")]
    [InlineData("", "message")]
    [InlineData("  ", "message")]
    [InlineData("code", null)]
    [InlineData("code", "")]
    [InlineData("code", "  ")]
    public void A_blank_code_or_message_is_rejected(string? code, string? message)
    {
        Assert.ThrowsAny<ArgumentException>(() => new OrionError(code!, message!));
    }

    [Fact]
    public void The_default_instance_is_not_a_usable_error()
    {
        // A readonly record struct always has a default; the contract is that packages never
        // hand one out - Error is null on success rather than default(OrionError).
        var uninitialised = default(OrionError);

        Assert.Null(uninitialised.Code);
        Assert.Null(uninitialised.Message);
    }

    [Fact]
    public void ToString_renders_code_and_message()
    {
        var error = new OrionError(OrionErrorCodes.NotFound, "No such key.");

        Assert.Equal("not_found: No such key.", error.ToString());
    }

    [Fact]
    public void ToString_includes_the_target_when_set()
    {
        var error = new OrionError(OrionErrorCodes.NotFound, "No such key.", "api-key-7");

        Assert.Equal("not_found (api-key-7): No such key.", error.ToString());
    }

    [Fact]
    public void Errors_compare_by_value()
    {
        var a = new OrionError(OrionErrorCodes.Timeout, "Too slow.", "acquire");
        var b = new OrionError(OrionErrorCodes.Timeout, "Too slow.", "acquire");
        var c = new OrionError(OrionErrorCodes.Timeout, "Too slow.", "renew");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Canonical_codes_are_unique_lowercase_snake_case()
    {
        string[] codes =
        [
            OrionErrorCodes.InvalidArgument,
            OrionErrorCodes.NotFound,
            OrionErrorCodes.Conflict,
            OrionErrorCodes.FailedPrecondition,
            OrionErrorCodes.Unauthenticated,
            OrionErrorCodes.PermissionDenied,
            OrionErrorCodes.RateLimited,
            OrionErrorCodes.Timeout,
            OrionErrorCodes.Cancelled,
            OrionErrorCodes.Unavailable,
            OrionErrorCodes.Internal,
        ];

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Matches("^[a-z]+(_[a-z]+)*$", code));
    }
}
