namespace DotnetYamlApiCompressor;

public static class SourceUrlBranchRewriter
{
    private const string MainSegment = "blob/main";

    public static void Rewrite(object? node, string branchLabel)
    {
        var targetBranch = branchLabel switch
        {
            "stable" => "master",
            "beta" => "beta",
            _ => branchLabel,
        };

        RewriteNode(node, targetBranch);
    }

    private static void RewriteNode(object? node, string targetBranch)
    {
        switch (node)
        {
            case Dictionary<object, object> dict:
                if (dict.GetValueOrDefault("sourceUrl") is string sourceUrl
                    && sourceUrl.Contains(MainSegment, StringComparison.Ordinal))
                {
                    dict["sourceUrl"] = sourceUrl.Replace(MainSegment, $"blob/{targetBranch}", StringComparison.Ordinal);
                }

                foreach (var value in dict.Values)
                {
                    RewriteNode(value, targetBranch);
                }
                break;

            case List<object> list:
                foreach (var item in list)
                {
                    RewriteNode(item, targetBranch);
                }
                break;
        }
    }
}
