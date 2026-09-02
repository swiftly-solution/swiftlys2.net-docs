using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace DotnetYamlApiCompressor;

public static class YamlDocumentLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    public static (List<JsonObject> Items, List<JsonObject> References) LoadDirectory(string inputDir)
    {
        var items = new List<JsonObject>();
        var references = new List<JsonObject>();

        if (!Directory.Exists(inputDir))
        {
            return (items, references);
        }

        var files = Directory.GetFiles(inputDir, "*.yml", SearchOption.TopDirectoryOnly)
            .Where(f => !string.Equals(Path.GetFileName(f), "toc.yml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var raw = Deserializer.Deserialize<object>(File.ReadAllText(file));
            if (JsonNodeConverter.ToJsonNode(raw) is not JsonObject root)
            {
                continue;
            }

            DrainArrayInto(root, "items", items);
            DrainArrayInto(root, "references", references);
        }

        return (items, references);
    }

    private static void DrainArrayInto(JsonObject root, string propertyName, List<JsonObject> target)
    {
        if (root[propertyName] is not JsonArray array)
        {
            return;
        }

        while (array.Count > 0)
        {
            var node = array[0];
            array.RemoveAt(0);
            if (node is JsonObject obj)
            {
                target.Add(obj);
            }
        }
    }
}
