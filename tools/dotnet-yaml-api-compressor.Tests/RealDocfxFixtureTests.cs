using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class RealDocfxFixtureTests
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static List<object> L(object o) => (List<object>)o;

    private static Dictionary<object, object> M(object o) => (Dictionary<object, object>)o;

    [Fact]
    public void LoadsRealNamespaceAndInterfacePagesIntoATree()
    {
        var pages = YamlDocumentLoader.LoadDirectory(FixturesDir);

        Assert.Equal(2, pages.Count);

        var root = ApiTreeBuilder.Build(pages, branchLabel: "stable");

        var categories = L(root["categories"]);
        Assert.Single(categories);
        var commands = M(categories[0]);
        Assert.Equal("Commands", commands["category"]);
        Assert.Equal("SwiftlyS2.Shared.Commands", commands["namespace"]);

        var types = L(commands["types"]);
        Assert.Single(types);
        var iCommandContext = M(types[0]);
        Assert.Equal("SwiftlyS2.Shared.Commands.ICommandContext", iCommandContext["uid"]);
        Assert.Equal("ICommandContext", iCommandContext["name"]);
        Assert.Equal("Interface", iCommandContext["type"]);

        // Real members split by kind, not preserved as raw presentation blocks.
        var properties = L(iCommandContext["properties"]);
        Assert.NotEmpty(properties);
        Assert.Contains(properties, p => M(p)["name"] as string == "Args");

        var methods = L(iCommandContext["methods"]);
        Assert.NotEmpty(methods);
        Assert.Contains(methods, m => M(m)["name"] as string == "Reply(string)");

        Assert.False(root.ContainsKey("other"));
    }
}
