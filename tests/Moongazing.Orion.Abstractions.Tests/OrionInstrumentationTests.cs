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

    [Fact]
    public void ActivitySource_is_versioned()
    {
        using var sut = new TestInstrumentation();
        Assert.Equal("9.9.9", sut.ActivitySource.Version);
    }

    [Fact]
    public void Constructor_rejects_a_null_name()
    {
        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentNullException (a subtype) for null.
        Assert.Throws<ArgumentNullException>(() => new ConfigurableInstrumentation(null!, "1.0.0"));
    }

    [Fact]
    public void Constructor_rejects_an_empty_name()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ConfigurableInstrumentation("", "1.0.0"));
        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void Constructor_rejects_a_null_version()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurableInstrumentation("Moongazing.OrionTest", null!));
    }

    [Fact]
    public void Constructor_rejects_an_empty_version()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ConfigurableInstrumentation("Moongazing.OrionTest", ""));
        Assert.Equal("version", ex.ParamName);
    }

    [Fact]
    public void SetStaticTags_rejects_null()
    {
        using var sut = new TestInstrumentation();
        Assert.Throws<ArgumentNullException>(() => sut.SetStaticTags(null!));
    }

    [Fact]
    public void SetStaticTags_replaces_rather_than_merges()
    {
        using var sut = new TestInstrumentation();
        sut.SetStaticTags(new Dictionary<string, string> { ["tenant"] = "acme" });
        sut.SetStaticTags(new Dictionary<string, string> { ["region"] = "eu" });

        // The second call wins entirely; "tenant" is gone, not merged.
        Assert.Single(sut.StaticTags);
        Assert.Contains(sut.StaticTags, t => t.Key == "region" && (string?)t.Value == "eu");
        Assert.DoesNotContain(sut.StaticTags, t => t.Key == "tenant");
    }

    [Fact]
    public void SetStaticTags_with_empty_dictionary_resets_to_empty()
    {
        using var sut = new TestInstrumentation();
        sut.SetStaticTags(new Dictionary<string, string> { ["tenant"] = "acme" });
        sut.SetStaticTags(new Dictionary<string, string>());

        Assert.Empty(sut.StaticTags);
        // And the allocation-light single-element path is restored.
        Assert.Single(sut.Tag(new KeyValuePair<string, object?>("k", "v")));
    }

    [Fact]
    public void StaticTags_snapshot_is_not_mutated_by_a_later_SetStaticTags()
    {
        using var sut = new TestInstrumentation();
        sut.SetStaticTags(new Dictionary<string, string> { ["tenant"] = "acme" });

        // Capture the snapshot an emission site would have read.
        var snapshot = sut.StaticTags;
        Assert.Single(snapshot);

        // A later reconfigure swaps the field for a new array; the captured one is untouched.
        sut.SetStaticTags(new Dictionary<string, string> { ["tenant"] = "acme", ["region"] = "eu" });

        Assert.Single(snapshot);
        Assert.Equal(2, sut.StaticTags.Length);
        Assert.NotSame(snapshot, sut.StaticTags);
    }

    [Fact]
    public void Tag_does_not_mutate_the_static_tag_snapshot()
    {
        using var sut = new TestInstrumentation();
        sut.SetStaticTags(new Dictionary<string, string> { ["tenant"] = "acme", ["region"] = "eu" });

        var before = sut.StaticTags;
        var beforeLength = before.Length;

        _ = sut.Tag(new KeyValuePair<string, object?>("outcome", "ok"));

        // Tag returns a fresh combined array; the underlying static-tag snapshot is unchanged.
        Assert.Equal(beforeLength, before.Length);
        Assert.Same(before, sut.StaticTags);
    }

    [Fact]
    public void Tag_preserves_static_tag_order_then_appends_the_extra_last()
    {
        using var sut = new TestInstrumentation();
        sut.SetStaticTags(new Dictionary<string, string> { ["tenant"] = "acme", ["region"] = "eu" });

        var tags = sut.Tag(new KeyValuePair<string, object?>("outcome", "ok"));

        Assert.Equal(3, tags.Length);
        // The per-measurement tag is always last.
        Assert.Equal("outcome", tags[2].Key);
    }

    [Fact]
    public void Tag_accepts_a_null_tag_value()
    {
        using var sut = new TestInstrumentation();
        var tags = sut.Tag(new KeyValuePair<string, object?>("k", null));
        Assert.Single(tags);
        Assert.Null(tags[0].Value);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var sut = new TestInstrumentation();
        sut.Dispose();
        // A second dispose must not throw (ActivitySource / Meter both tolerate it).
        sut.Dispose();
    }

    private sealed class ConfigurableInstrumentation : OrionInstrumentation
    {
        public ConfigurableInstrumentation(string name, string version) : base(name, version) { }
    }
}
