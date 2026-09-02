using System.Text.Json.Nodes;

namespace DotnetYamlApiCompressor;

public static class ApiTreeBuilder
{
    private static readonly HashSet<string> TypeKinds = new(StringComparer.Ordinal)
    {
        "Class", "Struct", "Interface", "Enum", "Delegate",
    };

    public static JsonObject Build(List<JsonObject> pages, string branchLabel)
    {
        var consumed = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        var namespaces = new JsonArray();
        var seenUids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ns in pages.Where(p => TypeOf(p) == "Namespace"))
        {
            consumed.Add(ns);
            var nsUid = UidOf(ns);
            ThrowIfDuplicateUid(seenUids, nsUid);
            var types = new JsonArray();

            foreach (var typePage in pages.Where(p => TypeKinds.Contains(TypeOf(p) ?? string.Empty) && nsUid is not null && ParentOf(p) == nsUid))
            {
                consumed.Add(typePage);
                ThrowIfDuplicateUid(seenUids, UidOf(typePage));
                types.Add(typePage);
            }

            ns["types"] = types;
            namespaces.Add(ns);
        }

        var other = new JsonArray();
        foreach (var page in pages.Where(p => !consumed.Contains(p)))
        {
            other.Add(page);
        }

        var root = new JsonObject
        {
            ["branch"] = branchLabel,
            ["namespaces"] = namespaces,
        };

        if (other.Count > 0)
        {
            root["other"] = other;
        }

        return root;
    }

    private static void ThrowIfDuplicateUid(HashSet<string> seenUids, string? uid)
    {
        if (uid is null)
        {
            return;
        }

        if (!seenUids.Add(uid))
        {
            throw new InvalidOperationException($"Duplicate uid encountered while building the API tree: '{uid}'. The same uid appears as a page in more than one docfx yml file.");
        }
    }

    private static string? TypeOf(JsonObject page) => page["type"]?.GetValue<string>();

    private static string? ParentOf(JsonObject page) => page["parent"]?.GetValue<string>();

    private static string? UidOf(JsonObject page) => page["uid"]?.GetValue<string>();
}
