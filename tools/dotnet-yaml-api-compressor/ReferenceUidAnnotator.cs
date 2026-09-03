namespace DotnetYamlApiCompressor;

public static class ReferenceUidAnnotator
{
    public static void Annotate(object? node)
    {
        switch (node)
        {
            case Dictionary<object, object> dict:
                if (!dict.ContainsKey("uid")
                    && dict.ContainsKey("text")
                    && dict.GetValueOrDefault("url") is string url
                    && TryDeriveUid(url, out var uid))
                {
                    dict["uid"] = uid;
                }

                foreach (var value in dict.Values)
                {
                    Annotate(value);
                }
                break;

            case List<object> list:
                foreach (var item in list)
                {
                    Annotate(item);
                }
                break;
        }
    }

    private static bool TryDeriveUid(string url, out string uid)
    {
        uid = string.Empty;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var withoutAnchor = url.Split('#', 2)[0];
        if (!withoutAnchor.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uid = withoutAnchor[..^".html".Length];
        return uid.Length > 0;
    }
}
