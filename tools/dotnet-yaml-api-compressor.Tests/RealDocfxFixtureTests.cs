using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class RealDocfxFixtureTests
{
    private static string FixturesDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    [Fact]
    public void LoadsRealNamespaceAndInterfacePagesIntoATree()
    {
        var pages = YamlDocumentLoader.LoadDirectory(FixturesDir);

        Assert.Equal(2, pages.Count);

        var root = ApiTreeBuilder.Build(pages, branchLabel: "stable");

        var namespaces = root["namespaces"]!.AsArray();
        Assert.Single(namespaces);
        var ns = namespaces[0]!.AsObject();
        Assert.Equal("SwiftlyS2.Shared.Commands", ns["uid"]!.GetValue<string>());

        var types = ns["types"]!.AsArray();
        Assert.Single(types);
        var iCommandContext = types[0]!.AsObject();
        Assert.Equal("SwiftlyS2.Shared.Commands.ICommandContext", iCommandContext["uid"]!.GetValue<string>());
        Assert.Equal("Interface", iCommandContext["type"]!.GetValue<string>());

        var sections = iCommandContext["body"]!["sections"]!.AsArray();
        Assert.Equal(2, sections.Count);
        Assert.Equal("Properties", sections[0]!["heading"]!["h2"]!.GetValue<string>());
        Assert.Equal("Methods", sections[1]!["heading"]!["h2"]!.GetValue<string>());

        Assert.False(root.ContainsKey("other"));
    }
}
