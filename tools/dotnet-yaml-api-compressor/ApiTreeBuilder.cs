using System.Text.Json.Nodes;

namespace DotnetYamlApiCompressor;

public static class ApiTreeBuilder
{
    private static readonly HashSet<string> TypeKinds = new(StringComparer.Ordinal)
    {
        "Class", "Struct", "Interface", "Enum", "Delegate",
    };

    private static readonly HashSet<string> MemberKinds = new(StringComparer.Ordinal)
    {
        "Method", "Property", "Field", "Event", "Constructor", "Operator",
    };

    public static JsonObject Build(List<JsonObject> items, List<JsonObject> references, string branchLabel)
    {
        var consumed = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        var namespaces = new JsonArray();
        var seenUids = new HashSet<string>(StringComparer.Ordinal);
        var childrenByParent = items.ToLookup(i => ParentOf(i) ?? string.Empty);

        foreach (var ns in items.Where(i => TypeOf(i) == "Namespace"))
        {
            consumed.Add(ns);
            var nsUid = UidOf(ns);
            ThrowIfDuplicateUid(seenUids, nsUid);
            var types = new JsonArray();

            foreach (var typeItem in childrenByParent[nsUid ?? string.Empty].Where(i => TypeKinds.Contains(TypeOf(i) ?? string.Empty)))
            {
                consumed.Add(typeItem);
                types.Add(BuildType(typeItem, childrenByParent, seenUids, consumed));
            }

            ns["types"] = types;
            namespaces.Add(ns);
        }

        var other = new JsonArray();
        foreach (var item in items.Where(i => !consumed.Contains(i)))
        {
            other.Add(item);
        }

        var referencesObj = new JsonObject();
        var seenReferenceUids = new HashSet<string>();
        foreach (var reference in references)
        {
            var uid = UidOf(reference);
            if (uid is null || !seenReferenceUids.Add(uid))
            {
                continue;
            }
            referencesObj[uid] = reference;
        }

        var root = new JsonObject
        {
            ["branch"] = branchLabel,
            ["namespaces"] = namespaces,
            ["references"] = referencesObj,
        };

        if (other.Count > 0)
        {
            root["other"] = other;
        }

        return root;
    }

    // Nests a type's immediate children: further nested types (recursively, so
    // a class nested inside a class inside a class resolves at any depth) and
    // members. Both keys are always present, even when empty, so every type
    // node has the same shape regardless of nesting depth.
    private static JsonObject BuildType(JsonObject typeItem, ILookup<string, JsonObject> childrenByParent, HashSet<string> seenUids, HashSet<JsonObject> consumed)
    {
        var typeUid = UidOf(typeItem);
        ThrowIfDuplicateUid(seenUids, typeUid);

        var nestedTypes = new JsonArray();
        var members = new JsonArray();

        foreach (var child in childrenByParent[typeUid ?? string.Empty])
        {
            var childKind = TypeOf(child);

            if (TypeKinds.Contains(childKind ?? string.Empty))
            {
                consumed.Add(child);
                nestedTypes.Add(BuildType(child, childrenByParent, seenUids, consumed));
            }
            else if (MemberKinds.Contains(childKind ?? string.Empty))
            {
                consumed.Add(child);
                var memberUid = UidOf(child);
                ThrowIfDuplicateUid(seenUids, memberUid);
                members.Add(child);
            }
        }

        typeItem["types"] = nestedTypes;
        typeItem["members"] = members;
        return typeItem;
    }

    private static void ThrowIfDuplicateUid(HashSet<string> seenUids, string? uid)
    {
        if (uid is null)
        {
            return;
        }

        if (!seenUids.Add(uid))
        {
            throw new InvalidOperationException($"Duplicate uid encountered while building the API tree: '{uid}'. The same uid appears as an item in more than one docfx yml file.");
        }
    }

    private static string? TypeOf(JsonObject item) => item["type"]?.GetValue<string>();

    private static string? ParentOf(JsonObject item) => item["parent"]?.GetValue<string>();

    private static string? UidOf(JsonObject item) => item["uid"]?.GetValue<string>();
}
