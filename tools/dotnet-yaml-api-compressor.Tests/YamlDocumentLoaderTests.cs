using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class YamlDocumentLoaderTests
{
    private static string WriteFixture(string dir, string fileName, string yaml)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, yaml);
        return path;
    }

    [Fact]
    public void LoadsItemsAndReferencesAndSkipsTocYml()
    {
        var dir = Path.Combine(Path.GetTempPath(), "loader-" + Guid.NewGuid());
        WriteFixture(dir, "My.Namespace.yml", """
            ### YamlMime:ManagedReference
            items:
            - uid: My.Namespace
              type: Namespace
              name: My.Namespace
            references:
            - uid: System.Object
              name: Object
            """);
        WriteFixture(dir, "toc.yml", """
            items:
            - uid: should-be-ignored
            """);

        var (items, references) = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Single(items);
        Assert.Equal("My.Namespace", items[0]["uid"]!.GetValue<string>());
        Assert.Single(references);
        Assert.Equal("System.Object", references[0]["uid"]!.GetValue<string>());

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void PreservesBooleanAndNumericScalarTypes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "loader-types-" + Guid.NewGuid());
        WriteFixture(dir, "My.Ns.Widget.yml", """
            ### YamlMime:ManagedReference
            items:
            - uid: My.Ns.Widget
              type: Class
              isEii: true
              isExternal: false
              startLine: 42
            """);

        var (items, _) = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Equal(System.Text.Json.JsonValueKind.True, items[0]["isEii"]!.AsValue().GetValueKind());
        Assert.Equal(System.Text.Json.JsonValueKind.False, items[0]["isExternal"]!.AsValue().GetValueKind());
        Assert.Equal(System.Text.Json.JsonValueKind.Number, items[0]["startLine"]!.AsValue().GetValueKind());
        Assert.Equal(42, int.Parse(items[0]["startLine"]!.ToJsonString()));

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void ReturnsEmptyListsWhenDirectoryMissing()
    {
        var (items, references) = YamlDocumentLoader.LoadDirectory(
            Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid()));

        Assert.Empty(items);
        Assert.Empty(references);
    }
}
