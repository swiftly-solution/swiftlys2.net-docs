using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class ReferenceUidAnnotatorTests
{
    [Fact]
    public void AddsUidBesideUrlForInternalReferencesAnywhereInTheTree()
    {
        var root = new Dictionary<object, object>
        {
            ["categories"] = new List<object>
            {
                new Dictionary<object, object>
                {
                    ["types"] = new List<object>
                    {
                        new Dictionary<object, object>
                        {
                            ["uid"] = "My.Ns.Widget",
                            ["implements"] = new List<object>
                            {
                                new Dictionary<object, object> { ["text"] = "IThing", ["url"] = "My.Ns.IThing.html" },
                            },
                            ["methods"] = new List<object>
                            {
                                new Dictionary<object, object>
                                {
                                    ["name"] = "Do(string)",
                                    ["parameters"] = new List<object>
                                    {
                                        new Dictionary<object, object>
                                        {
                                            ["name"] = "value",
                                            ["type"] = new List<object>
                                            {
                                                new Dictionary<object, object> { ["text"] = "string", ["url"] = "https://learn.microsoft.com/dotnet/api/system.string" },
                                            },
                                        },
                                    },
                                    ["returns"] = new Dictionary<object, object>
                                    {
                                        ["type"] = new List<object>
                                        {
                                            new Dictionary<object, object> { ["text"] = "Other", ["url"] = "My.Ns.Other.html" },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        ReferenceUidAnnotator.Annotate(root);

        var types = (List<object>)((Dictionary<object, object>)((List<object>)root["categories"])[0])["types"];
        var widget = (Dictionary<object, object>)types[0];

        var implementsRef = (Dictionary<object, object>)((List<object>)widget["implements"])[0];
        Assert.Equal("My.Ns.IThing", implementsRef["uid"]);
        Assert.Equal("My.Ns.IThing.html", implementsRef["url"]);

        var method = (Dictionary<object, object>)((List<object>)widget["methods"])[0];
        var paramType = (Dictionary<object, object>)((List<object>)((Dictionary<object, object>)((List<object>)method["parameters"])[0])["type"])[0];
        Assert.False(paramType.ContainsKey("uid"));

        var returnsType = (Dictionary<object, object>)((List<object>)((Dictionary<object, object>)method["returns"])["type"])[0];
        Assert.Equal("My.Ns.Other", returnsType["uid"]);
    }

    [Fact]
    public void StripsMemberAnchorWhenDerivingUidFromHref()
    {
        var reference = new Dictionary<object, object> { ["text"] = "Dispose()", ["url"] = "My.Ns.Base.html#My_Ns_Base_Dispose" };

        ReferenceUidAnnotator.Annotate(reference);

        Assert.Equal("My.Ns.Base", reference["uid"]);
    }

    [Fact]
    public void DoesNotOverwriteAnExistingUid()
    {
        var reference = new Dictionary<object, object> { ["text"] = "X", ["url"] = "My.Ns.X.html", ["uid"] = "already-set" };

        ReferenceUidAnnotator.Annotate(reference);

        Assert.Equal("already-set", reference["uid"]);
    }
}
