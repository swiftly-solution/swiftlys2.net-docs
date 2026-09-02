using System.Text.Json.Nodes;
using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class ApiTreeBuilderTests
{
    private static JsonObject Page(string uid, string type, string? parent = null)
    {
        var obj = new JsonObject { ["uid"] = uid, ["type"] = type, ["title"] = $"{type} {uid}" };
        if (parent is not null) obj["parent"] = parent;
        return obj;
    }

    [Fact]
    public void NestsTypesUnderTheirNamespace()
    {
        var pages = new List<JsonObject>
        {
            Page("My.Ns", "Namespace"),
            Page("My.Ns.Widget", "Class", parent: "My.Ns"),
            Page("My.Ns.IThing", "Interface", parent: "My.Ns"),
        };

        var root = ApiTreeBuilder.Build(pages, branchLabel: "stable");

        var namespaces = root["namespaces"]!.AsArray();
        Assert.Single(namespaces);
        var ns = namespaces[0]!.AsObject();
        Assert.Equal("My.Ns", ns["uid"]!.GetValue<string>());

        var types = ns["types"]!.AsArray();
        Assert.Equal(2, types.Count);
        Assert.Contains(types, t => t!["uid"]!.GetValue<string>() == "My.Ns.Widget");
        Assert.Contains(types, t => t!["uid"]!.GetValue<string>() == "My.Ns.IThing");

        Assert.Equal("stable", root["branch"]!.GetValue<string>());
        Assert.False(root.ContainsKey("other"));
        Assert.False(root.ContainsKey("references"));
    }

    [Fact]
    public void PutsUnresolvedPagesInOtherBucket()
    {
        var pages = new List<JsonObject>
        {
            Page("orphan.type", "Class", parent: "does.not.exist"),
        };

        var root = ApiTreeBuilder.Build(pages, branchLabel: "beta");

        Assert.Empty(root["namespaces"]!.AsArray());
        var other = root["other"]!.AsArray();
        Assert.Single(other);
        Assert.Equal("orphan.type", other[0]!["uid"]!.GetValue<string>());
    }

    [Fact]
    public void ThrowsClearExceptionOnDuplicateUidAcrossFiles()
    {
        var pages = new List<JsonObject>
        {
            Page("My.Ns", "Namespace"),
            Page("My.Ns.Widget", "Class", parent: "My.Ns"),
            Page("My.Ns.Widget", "Class", parent: "My.Ns"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ApiTreeBuilder.Build(pages, branchLabel: "stable"));
        Assert.Contains("My.Ns.Widget", ex.Message);
    }
}
