namespace DotnetYamlApiCompressor;

public static class ApiTreeBuilder
{
    private static readonly HashSet<string> TypeKinds = new(StringComparer.Ordinal)
    {
        "Class", "Struct", "Interface", "Enum", "Delegate",
    };

    private static readonly string[] NamespacePrefixesToStrip =
    {
        "SwiftlyS2.Core.", "SwiftlyS2.Shared.", "SwiftlyS2.",
    };

    public static Dictionary<object, object> Build(List<Dictionary<object, object>> pages, string branchLabel)
    {
        var consumed = new HashSet<Dictionary<object, object>>(ReferenceEqualityComparer.Instance);
        var categories = new List<object>();
        var seenUids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var ns in pages.Where(p => TypeOf(p) == "Namespace"))
        {
            consumed.Add(ns);
            var nsUid = UidOf(ns);
            ThrowIfDuplicateUid(seenUids, nsUid);
            var types = new List<object>();

            foreach (var typePage in pages.Where(p => TypeKinds.Contains(TypeOf(p) ?? string.Empty) && nsUid is not null && ParentOf(p) == nsUid))
            {
                consumed.Add(typePage);
                var typeUid = UidOf(typePage);
                ThrowIfDuplicateUid(seenUids, typeUid);

                if (nsUid is not null && typeUid is not null && typeUid.StartsWith(nsUid + ".", StringComparison.Ordinal))
                {
                    typePage["name"] = typeUid[(nsUid.Length + 1)..];
                }

                types.Add(typePage);
            }

            categories.Add(new Dictionary<object, object>
            {
                ["category"] = nsUid is null ? string.Empty : ToCategoryName(nsUid),
                ["namespace"] = nsUid ?? string.Empty,
                ["types"] = types,
            });
        }

        var other = new List<object>();
        foreach (var page in pages.Where(p => !consumed.Contains(p)))
        {
            other.Add(page);
        }

        var root = new Dictionary<object, object>
        {
            ["branch"] = branchLabel,
            ["categories"] = categories,
        };

        if (other.Count > 0)
        {
            root["other"] = other;
        }

        return root;
    }

    private static string ToCategoryName(string namespaceUid)
    {
        foreach (var prefix in NamespacePrefixesToStrip)
        {
            if (namespaceUid.StartsWith(prefix, StringComparison.Ordinal))
            {
                return namespaceUid[prefix.Length..];
            }
        }

        return namespaceUid;
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

    private static string? TypeOf(Dictionary<object, object> page) => page.GetValueOrDefault("type") as string;

    private static string? ParentOf(Dictionary<object, object> page) => page.GetValueOrDefault("parent") as string;

    private static string? UidOf(Dictionary<object, object> page) => page.GetValueOrDefault("uid") as string;
}
