namespace Moongazing.Orion.Abstractions.Tests;

using Moongazing.Orion.Abstractions.Diagnostics;
using Xunit;

public sealed class OrionInstrumentationTests
{
    private sealed class TestInstrumentation : OrionInstrumentation
    {
        public TestInstrumentation() : base("Moongazing.OrionTest", "9.9.9") { }
    }

    [Fact]
    public void Names_the_activity_source_and_meter_consistently()
    {
        using var sut = new TestInstrumentation();
        Assert.Equal("Moongazing.OrionTest", sut.ActivitySource.Name);
        Assert.Equal("Moongazing.OrionTest", sut.Meter.Name);
        Assert.Equal("9.9.9", sut.Meter.Version);
    }

    [Fact]
    public void Tag_with_no_static_tags_returns_a_single_element_array()
    {
        using var sut = new TestInstrumentation();
        var tags = sut.Tag(new KeyValuePair<string, object?>("k", "v"));
        Assert.Single(tags);
        Assert.Equal("k", tags[0].Key);
    }

    [Fact]
    public void Tag_appends_to_configured_static_tags()
    {
        using var sut = new TestInstrumentation();
        sut.SetStaticTags(new Dictionary<string, string> { ["tenant"] = "acme", ["region"] = "eu" });

        var tags = sut.Tag(new KeyValuePair<string, object?>("outcome", "ok"));

        Assert.Equal(3, tags.Length);
        Assert.Contains(tags, t => t.Key == "tenant" && (string?)t.Value == "acme");
        Assert.Contains(tags, t => t.Key == "region" && (string?)t.Value == "eu");
        Assert.Contains(tags, t => t.Key == "outcome" && (string?)t.Value == "ok");
    }

    [Fact]
    public void StaticTags_default_is_empty()
    {
        using var sut = new TestInstrumentation();
        Assert.Empty(sut.StaticTags);
    }
}
