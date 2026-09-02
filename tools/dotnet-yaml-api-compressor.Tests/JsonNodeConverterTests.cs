using System.Text.Json.Nodes;
using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class JsonNodeConverterTests
{
    [Fact]
    public void ConvertsNestedMapsAndListsAndScalars()
    {
        var input = new Dictionary<object, object>
        {
            ["name"] = "Foo",
            ["count"] = 3,
            ["enabled"] = true,
            ["children"] = new List<object> { "a", "b" },
            ["nested"] = new Dictionary<object, object> { ["inner"] = "value" },
        };

        var node = JsonNodeConverter.ToJsonNode(input) as JsonObject;

        Assert.NotNull(node);
        Assert.Equal("Foo", node!["name"]!.GetValue<string>());
        Assert.Equal(3, node["count"]!.GetValue<int>());
        Assert.True(node["enabled"]!.GetValue<bool>());
        Assert.Equal(2, node["children"]!.AsArray().Count);
        Assert.Equal("value", node["nested"]!["inner"]!.GetValue<string>());
    }

    [Fact]
    public void ConvertsNullToNull()
    {
        Assert.Null(JsonNodeConverter.ToJsonNode(null));
    }
}
