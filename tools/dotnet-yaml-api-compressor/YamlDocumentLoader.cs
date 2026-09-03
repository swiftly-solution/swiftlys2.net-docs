using YamlDotNet.Serialization;

namespace DotnetYamlApiCompressor;

public static class YamlDocumentLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    private static readonly HashSet<string> ExcludedNamespaces = new(StringComparer.Ordinal)
    {
        "SwiftlyS2.Shared.SchemaDefinitions",
        "SwiftlyS2.Shared.ProtobufDefinitions",
        "SwiftlyS2.Shared.GameEventDefinitions"
    };

    public static List<Dictionary<object, object>> LoadDirectory(string inputDir)
    {
        var pages = new List<Dictionary<object, object>>();

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
            if (Deserializer.Deserialize<object>(File.ReadAllText(file)) is not Dictionary<object, object> page)
            {
                continue;
            }

            Annotate(page);

            if (IsExcluded(page))
            {
                continue;
            }

            pages.Add(page);
        }

        return pages;
    }

    private static bool IsExcluded(Dictionary<object, object> page)
    {
        var uid = page.GetValueOrDefault("uid") as string;
        var parent = page.GetValueOrDefault("parent") as string;

        return (uid is not null && ExcludedNamespaces.Contains(uid))
            || (parent is not null && ExcludedNamespaces.Contains(parent));
    }

    private static void Annotate(Dictionary<object, object> page)
    {
        if (page.GetValueOrDefault("title") is not string title || page.GetValueOrDefault("body") is not List<object> body)
        {
            return;
        }

        var kind = title.Split(' ', 2)[0];
        page["type"] = kind;

        var uid = FindPageUid(body);
        if (uid is not null)
        {
            page["uid"] = uid;
        }

        if (kind != "Namespace")
        {
            var namespaceUid = FindNamespaceFact(body);
            if (namespaceUid is not null)
            {
                page["parent"] = namespaceUid;
            }

            foreach (var (key, value) in TypePageStructurer.Structure(body))
            {
                page[key] = value;
            }
        }

        page.Remove("title");
        page.Remove("body");
        page.Remove("languageId");
        page.Remove("metadata");
    }

    private static string? FindPageUid(List<object> body)
    {
        if (body.Count == 0
            || body[0] is not Dictionary<object, object> header
            || header.GetValueOrDefault("metadata") is not Dictionary<object, object> metadata)
        {
            return null;
        }

        return metadata.GetValueOrDefault("uid") as string;
    }

    private static string? FindNamespaceFact(List<object> body)
    {
        foreach (var block in body)
        {
            if (block is not Dictionary<object, object> obj || obj.GetValueOrDefault("facts") is not List<object> facts)
            {
                continue;
            }

            foreach (var fact in facts)
            {
                if (fact is Dictionary<object, object> factObj
                    && factObj.GetValueOrDefault("name") as string == "Namespace"
                    && factObj.GetValueOrDefault("value") is Dictionary<object, object> value
                    && value.GetValueOrDefault("text") is string namespaceUid)
                {
                    return namespaceUid;
                }
            }
        }

        return null;
    }
}
