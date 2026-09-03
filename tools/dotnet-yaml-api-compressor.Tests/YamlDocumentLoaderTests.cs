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
    public void AnnotatesNamespacePageWithTypeAndUidButNoParent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "loader-" + Guid.NewGuid());
        WriteFixture(dir, "My.Ns.yml", """
            ### YamlMime:ApiPage
            title: Namespace My.Ns
            body:
            - api1: Namespace My.Ns
              metadata:
                uid: My.Ns
                commentId: N:My.Ns
            """);

        var pages = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Single(pages);
        Assert.Equal("Namespace", pages[0]["type"]);
        Assert.Equal("My.Ns", pages[0]["uid"]);
        Assert.False(pages[0].ContainsKey("parent"));

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void AnnotatesTypePageWithUidAndParentFromNamespaceFact()
    {
        var dir = Path.Combine(Path.GetTempPath(), "loader-" + Guid.NewGuid());
        WriteFixture(dir, "My.Ns.Widget.yml", """
            ### YamlMime:ApiPage
            title: Class Widget
            body:
            - api1: Class Widget
              metadata:
                uid: My.Ns.Widget
                commentId: T:My.Ns.Widget
            - facts:
              - name: Namespace
                value:
                  text: My.Ns
                  url: My.Ns.html
              - name: Assembly
                value: My.Ns.dll
            """);

        var pages = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Single(pages);
        Assert.Equal("Class", pages[0]["type"]);
        Assert.Equal("My.Ns.Widget", pages[0]["uid"]);
        Assert.Equal("My.Ns", pages[0]["parent"]);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void NestedTypeStillPointsAtTopLevelNamespaceNotItsEnclosingType()
    {
        // Mirrors real docfx ApiPage output: a delegate nested inside an
        // interface (ICommandService.ClientChatHandler in the real fixtures)
        // still reports the TRUE top-level namespace in its own "Namespace"
        // fact, not the enclosing interface — docfx flattens nested types
        // this way for ApiPage output.
        var dir = Path.Combine(Path.GetTempPath(), "loader-" + Guid.NewGuid());
        WriteFixture(dir, "My.Ns.IOuter.Inner.yml", """
            ### YamlMime:ApiPage
            title: Delegate IOuter.Inner
            body:
            - api1: Delegate IOuter.Inner
              metadata:
                uid: My.Ns.IOuter.Inner
                commentId: T:My.Ns.IOuter.Inner
            - facts:
              - name: Namespace
                value:
                  text: My.Ns
                  url: My.Ns.html
            """);

        var pages = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Equal("My.Ns", pages[0]["parent"]);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void PreservesGenericUidWithBacktickFromMetadataNotFilename()
    {
        // Mirrors SwiftlyS2.Shared.Convars.IConVar-1.yml in the real
        // fixtures: filename mangles the generic arity as "-1", but the
        // real uid in metadata uses a backtick.
        var dir = Path.Combine(Path.GetTempPath(), "loader-" + Guid.NewGuid());
        WriteFixture(dir, "My.Ns.IThing-1.yml", """
            ### YamlMime:ApiPage
            title: Interface IThing<T>
            body:
            - api1: Interface IThing<T>
              metadata:
                uid: My.Ns.IThing`1
                commentId: T:My.Ns.IThing`1
            - facts:
              - name: Namespace
                value:
                  text: My.Ns
                  url: My.Ns.html
            """);

        var pages = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Equal("My.Ns.IThing`1", pages[0]["uid"]);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void SkipsTocYmlAndPicksUpDotYamlExtensionToo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "loader-" + Guid.NewGuid());
        WriteFixture(dir, "toc.yml", "items:\n- uid: should-be-ignored\n");
        WriteFixture(dir, "My.Ns.yml", """
            title: Namespace My.Ns
            body:
            - api1: Namespace My.Ns
              metadata:
                uid: My.Ns
            """);
        WriteFixture(dir, "My.Ns.Other.yaml", """
            title: Class Other
            body:
            - api1: Class Other
              metadata:
                uid: My.Ns.Other
            - facts:
              - name: Namespace
                value:
                  text: My.Ns
            """);

        var pages = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Equal(2, pages.Count);
        Assert.DoesNotContain(pages, p => p.GetValueOrDefault("uid") as string == "should-be-ignored");
        Assert.Contains(pages, p => p.GetValueOrDefault("uid") as string == "My.Ns.Other");

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void PreservesBooleanAndNumericScalarTypes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "loader-types-" + Guid.NewGuid());
        WriteFixture(dir, "scalars.yml", """
            uid: My.Ns.Widget
            isEii: true
            isExternal: false
            startLine: 42
            """);

        var pages = YamlDocumentLoader.LoadDirectory(dir);

        Assert.IsType<bool>(pages[0]["isEii"]);
        Assert.Equal(true, pages[0]["isEii"]);
        Assert.IsType<bool>(pages[0]["isExternal"]);
        Assert.Equal(false, pages[0]["isExternal"]);
        Assert.Equal(42L, Convert.ToInt64(pages[0]["startLine"]));

        Directory.Delete(dir, recursive: true);
    }

    [Theory]
    [InlineData("SwiftlyS2.Shared.SchemaDefinitions", "C_OP_Widget")]
    [InlineData("SwiftlyS2.Shared.ProtobufDefinitions", "SomeMessage")]
    public void ExcludesKnownNoiseNamespacesAndTheirTypesEntirely(string excludedNamespace, string typeName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "loader-" + Guid.NewGuid());
        WriteFixture(dir, $"{excludedNamespace}.yml", $"""
            title: Namespace {excludedNamespace}
            body:
            - api1: Namespace {excludedNamespace}
              metadata:
                uid: {excludedNamespace}
            """);
        WriteFixture(dir, $"{excludedNamespace}.{typeName}.yml", $"""
            title: Class {typeName}
            body:
            - api1: Class {typeName}
              metadata:
                uid: {excludedNamespace}.{typeName}
            - facts:
              - name: Namespace
                value:
                  text: {excludedNamespace}
            """);
        WriteFixture(dir, "My.Ns.yml", """
            title: Namespace My.Ns
            body:
            - api1: Namespace My.Ns
              metadata:
                uid: My.Ns
            """);

        var pages = YamlDocumentLoader.LoadDirectory(dir);

        Assert.Single(pages);
        Assert.Equal("My.Ns", pages[0]["uid"]);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void ReturnsEmptyListWhenDirectoryMissing()
    {
        var pages = YamlDocumentLoader.LoadDirectory(
            Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid()));

        Assert.Empty(pages);
    }
}
