using System.Text.Json.Nodes;
using YamlDotNet.Serialization;

namespace DotnetYamlApiCompressor;

public static class YamlDocumentLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    public static List<JsonObject> LoadDirectory(string inputDir)
    {
        var pages = new List<JsonObject>();

        if (!Directory.Exists(inputDir))
        {
            return pages;
        }

        var files = Directory.EnumerateFiles(inputDir, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !string.Equals(Path.GetFileNameWithoutExtension(f), "toc", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);

        foreach (var file in files)
        {
            var raw = Deserializer.Deserialize<object>(File.ReadAllText(file));
            if (JsonNodeConverter.ToJsonNode(raw) is not JsonObject page)
            {
                continue;
            }

            Annotate(page);
            pages.Add(page);
        }

        return pages;
    }

    private static void Annotate(JsonObject page)
    {
        var title = page["title"]?.GetValue<string>();
        if (title is null || page["body"] is not JsonArray body)
        {
            return;
        }

        page["type"] = title.Split(' ', 2)[0];

        var uid = FindPageUid(body);
        if (uid is not null)
        {
            page["uid"] = uid;
        }

        var namespaceUid = FindNamespaceFact(body);
        if (namespaceUid is not null)
        {
            page["parent"] = namespaceUid;
        }

        page["body"] = ApiPageParser.Parse(body);
    }

    private static string? FindPageUid(JsonArray body)
    {
        if (body.Count == 0 || body[0] is not JsonObject header || header["metadata"] is not JsonObject metadata)
        {
            return null;
        }

        return metadata["uid"]?.GetValue<string>();
    }

    private static string? FindNamespaceFact(JsonArray body)
    {
        foreach (var block in body)
        {
            if (block is not JsonObject obj || obj["facts"] is not JsonArray facts)
            {
                continue;
            }

            foreach (var fact in facts)
            {
                if (fact is JsonObject factObj
                    && factObj["name"]?.GetValue<string>() == "Namespace"
                    && factObj["value"] is JsonObject value
                    && value["text"]?.GetValue<string>() is string namespaceUid)
                {
                    return namespaceUid;
                }
            }
        }

        return null;
    }
}
