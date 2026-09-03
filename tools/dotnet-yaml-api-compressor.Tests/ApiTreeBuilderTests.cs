using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class ApiTreeBuilderTests
{
    private static Dictionary<object, object> Page(string uid, string type, string? parent = null)
    {
        var obj = new Dictionary<object, object> { ["uid"] = uid, ["type"] = type };
        if (parent is not null) obj["parent"] = parent;
        return obj;
    }

    private static List<object> L(object o) => (List<object>)o;

    private static Dictionary<object, object> M(object o) => (Dictionary<object, object>)o;

    [Fact]
    public void NestsTypesUnderTheirNamespaceCategoryAndStripsKnownPrefix()
    {
        var pages = new List<Dictionary<object, object>>
        {
            Page("SwiftlyS2.Shared.Commands", "Namespace"),
            Page("SwiftlyS2.Shared.Commands.Command", "Class", parent: "SwiftlyS2.Shared.Commands"),
            Page("SwiftlyS2.Shared.Commands.ICommandContext", "Interface", parent: "SwiftlyS2.Shared.Commands"),
        };

        var root = ApiTreeBuilder.Build(pages, branchLabel: "stable");

        var categories = L(root["categories"]);
        Assert.Single(categories);
        var commands = M(categories[0]);
        Assert.Equal("Commands", commands["category"]);
        Assert.Equal("SwiftlyS2.Shared.Commands", commands["namespace"]);

        var types = L(commands["types"]);
        Assert.Equal(2, types.Count);
        Assert.Contains(types, t => M(t)["uid"] as string == "SwiftlyS2.Shared.Commands.Command" && M(t)["name"] as string == "Command");
        Assert.Contains(types, t => M(t)["uid"] as string == "SwiftlyS2.Shared.Commands.ICommandContext" && M(t)["name"] as string == "ICommandContext");

        Assert.Equal("stable", root["branch"]);
        Assert.False(root.ContainsKey("other"));
    }

    [Fact]
    public void NestedTypeNameKeepsEnclosingTypeQualifierAfterStrippingNamespace()
    {
        var pages = new List<Dictionary<object, object>>
        {
            Page("SwiftlyS2.Shared.Commands", "Namespace"),
            Page("SwiftlyS2.Shared.Commands.ICommandService.ClientChatHandler", "Delegate", parent: "SwiftlyS2.Shared.Commands"),
        };

        var root = ApiTreeBuilder.Build(pages, branchLabel: "stable");

        var types = L(M(L(root["categories"])[0])["types"]);
        Assert.Equal("ICommandService.ClientChatHandler", M(types[0])["name"]);
    }

    [Fact]
    public void KeepsFullNamespaceAsCategoryWhenNoKnownPrefixMatches()
    {
        var pages = new List<Dictionary<object, object>>
        {
            Page("Unrelated.Ns", "Namespace"),
        };

        var root = ApiTreeBuilder.Build(pages, branchLabel: "stable");

        var category = M(L(root["categories"])[0]);
        Assert.Equal("Unrelated.Ns", category["category"]);
    }

    [Fact]
    public void PutsUnresolvedPagesInOtherBucket()
    {
        var pages = new List<Dictionary<object, object>>
        {
            Page("orphan.type", "Class", parent: "does.not.exist"),
        };

        var root = ApiTreeBuilder.Build(pages, branchLabel: "beta");

        Assert.Empty(L(root["categories"]));
        var other = L(root["other"]);
        Assert.Single(other);
        Assert.Equal("orphan.type", M(other[0])["uid"]);
    }

    [Fact]
    public void ThrowsClearExceptionOnDuplicateUidAcrossFiles()
    {
        var pages = new List<Dictionary<object, object>>
        {
            Page("My.Ns", "Namespace"),
            Page("My.Ns.Widget", "Class", parent: "My.Ns"),
            Page("My.Ns.Widget", "Class", parent: "My.Ns"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ApiTreeBuilder.Build(pages, branchLabel: "stable"));
        Assert.Contains("My.Ns.Widget", ex.Message);
    }
}
