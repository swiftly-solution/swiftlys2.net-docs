using System.Text.Json.Nodes;
using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class ApiTreeBuilderTests
{
    private static JsonObject Item(string uid, string type, string? parent = null)
    {
        var obj = new JsonObject { ["uid"] = uid, ["type"] = type };
        if (parent is not null) obj["parent"] = parent;
        return obj;
    }

    [Fact]
    public void NestsNamespaceTypeAndMembers()
    {
        var items = new List<JsonObject>
        {
            Item("My.Ns", "Namespace"),
            Item("My.Ns.Widget", "Class", parent: "My.Ns"),
            Item("My.Ns.Widget.DoThing", "Method", parent: "My.Ns.Widget"),
        };

        var root = ApiTreeBuilder.Build(items, references: new List<JsonObject>(), branchLabel: "stable");

        var namespaces = root["namespaces"]!.AsArray();
        Assert.Single(namespaces);

        var ns = namespaces[0]!.AsObject();
        Assert.Equal("My.Ns", ns["uid"]!.GetValue<string>());

        var types = ns["types"]!.AsArray();
        Assert.Single(types);
        var type = types[0]!.AsObject();
        Assert.Equal("My.Ns.Widget", type["uid"]!.GetValue<string>());

        var members = type["members"]!.AsArray();
        Assert.Single(members);
        Assert.Equal("My.Ns.Widget.DoThing", members[0]!["uid"]!.GetValue<string>());

        Assert.Equal("stable", root["branch"]!.GetValue<string>());
        Assert.False(root.ContainsKey("other"));
    }

    [Fact]
    public void PutsUnresolvedItemsInOtherBucket()
    {
        var items = new List<JsonObject>
        {
            Item("orphan.method", "Method", parent: "does.not.exist"),
        };

        var root = ApiTreeBuilder.Build(items, references: new List<JsonObject>(), branchLabel: "beta");

        Assert.Empty(root["namespaces"]!.AsArray());
        var other = root["other"]!.AsArray();
        Assert.Single(other);
        Assert.Equal("orphan.method", other[0]!["uid"]!.GetValue<string>());
    }

    [Fact]
    public void ThrowsClearExceptionOnDuplicateUidAcrossFiles()
    {
        var items = new List<JsonObject>
        {
            Item("My.Ns", "Namespace"),
            Item("My.Ns.Widget", "Class", parent: "My.Ns"),
            Item("My.Ns.Widget", "Class", parent: "My.Ns"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ApiTreeBuilder.Build(items, references: new List<JsonObject>(), branchLabel: "stable"));

        Assert.Contains("My.Ns.Widget", ex.Message);
    }

    [Fact]
    public void DedupesReferencesByUid()
    {
        var references = new List<JsonObject>
        {
            new() { ["uid"] = "System.Object", ["name"] = "Object" },
            new() { ["uid"] = "System.Object", ["name"] = "Object (duplicate)" },
        };

        var root = ApiTreeBuilder.Build(items: new List<JsonObject>(), references, branchLabel: "stable");

        var referencesObj = root["references"]!.AsObject();
        Assert.Single(referencesObj);
        Assert.Equal("Object", referencesObj["System.Object"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void ResolvesNestedTypesAtAnyDepth()
    {
        var items = new List<JsonObject>
        {
            Item("My.Ns", "Namespace"),
            Item("My.Ns.Outer", "Class", parent: "My.Ns"),
            Item("My.Ns.Outer.OuterMethod", "Method", parent: "My.Ns.Outer"),
            Item("My.Ns.Outer.Inner", "Class", parent: "My.Ns.Outer"),
            Item("My.Ns.Outer.Inner.InnerMethod", "Method", parent: "My.Ns.Outer.Inner"),
            Item("My.Ns.Outer.Inner.Deepest", "Enum", parent: "My.Ns.Outer.Inner"),
        };

        var root = ApiTreeBuilder.Build(items, references: new List<JsonObject>(), branchLabel: "stable");

        var outer = root["namespaces"]!.AsArray()[0]!.AsObject()["types"]!.AsArray()[0]!.AsObject();
        Assert.Equal("My.Ns.Outer", outer["uid"]!.GetValue<string>());
        Assert.Single(outer["members"]!.AsArray());
        Assert.Equal("My.Ns.Outer.OuterMethod", outer["members"]![0]!["uid"]!.GetValue<string>());

        var inner = outer["types"]!.AsArray()[0]!.AsObject();
        Assert.Equal("My.Ns.Outer.Inner", inner["uid"]!.GetValue<string>());
        Assert.Single(inner["members"]!.AsArray());
        Assert.Equal("My.Ns.Outer.Inner.InnerMethod", inner["members"]![0]!["uid"]!.GetValue<string>());
        Assert.Single(inner["types"]!.AsArray());
        Assert.Equal("My.Ns.Outer.Inner.Deepest", inner["types"]![0]!["uid"]!.GetValue<string>());

        var deepest = inner["types"]![0]!.AsObject();
        Assert.Empty(deepest["members"]!.AsArray());
        Assert.Empty(deepest["types"]!.AsArray());
        Assert.False(root.ContainsKey("other"));
    }
}
