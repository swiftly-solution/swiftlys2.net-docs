using System.Text.RegularExpressions;

namespace DotnetYamlApiCompressor;

public static class TypePageStructurer
{
    private static readonly Regex EmbeddedLineBreaks = new(@"\s*\r?\n\s*", RegexOptions.Compiled);
    private static readonly Regex RepeatedSpaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

    private static object NormalizeText(object value)
    {
        if (value is not string text)
        {
            return value;
        }

        var collapsed = EmbeddedLineBreaks.Replace(text, " ").Replace('\t', ' ');
        return RepeatedSpaces.Replace(collapsed, " ").Trim();
    }

    public static Dictionary<object, object> Structure(List<object> body)
    {
        var result = new Dictionary<object, object>();
        var categories = new Dictionary<string, List<object>>(StringComparer.Ordinal);

        Dictionary<object, object>? currentMember = null;
        string? currentCategory = null;
        string? currentH4 = null;
        var inRemarksSection = false;

        foreach (var node in body)
        {
            if (node is not Dictionary<object, object> block)
            {
                continue;
            }

            if (block.TryGetValue("h2", out var h2Value) && h2Value is string h2)
            {
                currentMember = null;
                currentH4 = null;
                inRemarksSection = string.Equals(h2, "Remarks", StringComparison.Ordinal);
                currentCategory = inRemarksSection ? null : h2.ToLowerInvariant();
                continue;
            }

            if (block.TryGetValue("h4", out var h4Value) && h4Value is string h4)
            {
                currentH4 = h4;
                continue;
            }

            if (block.ContainsKey("api1"))
            {
                if (block.GetValueOrDefault("src") is string typeSrc)
                {
                    result["sourceUrl"] = typeSrc;
                }
                continue;
            }

            if (block.TryGetValue("api3", out var api3Value) && api3Value is string name)
            {
                currentH4 = null;
                currentMember = new Dictionary<object, object> { ["name"] = name };
                if (block.GetValueOrDefault("metadata") is Dictionary<object, object> memberMetadata
                    && memberMetadata.GetValueOrDefault("uid") is string memberUid)
                {
                    currentMember["uid"] = memberUid;
                }
                if (block.GetValueOrDefault("src") is string memberSrc)
                {
                    currentMember["sourceUrl"] = memberSrc;
                }

                if (currentCategory is not null)
                {
                    if (!categories.TryGetValue(currentCategory, out var memberList))
                    {
                        memberList = new List<object>();
                        categories[currentCategory] = memberList;
                    }
                    memberList.Add(currentMember);
                }
                continue;
            }

            if (block.TryGetValue("code", out var codeValue))
            {
                if (currentMember is not null)
                {
                    currentMember["declaration"] = NormalizeText(codeValue);
                }
                else
                {
                    result["declaration"] = NormalizeText(codeValue);
                }
                continue;
            }

            if (block.TryGetValue("markdown", out var markdownValue))
            {
                if (currentMember is not null)
                {
                    if (currentH4 == "Remarks")
                    {
                        currentMember["remarks"] = NormalizeText(markdownValue);
                    }
                    else
                    {
                        currentMember["summary"] = NormalizeText(markdownValue);
                    }
                }
                else if (inRemarksSection)
                {
                    result["remarks"] = NormalizeText(markdownValue);
                }
                else
                {
                    result["summary"] = NormalizeText(markdownValue);
                }
                continue;
            }

            if (block.TryGetValue("inheritance", out var inheritanceValue) && inheritanceValue is List<object> inheritance)
            {
                if (inheritance.Count > 0)
                {
                    result["inherits"] = inheritance.GetRange(0, inheritance.Count - 1);
                }
                continue;
            }

            if (block.TryGetValue("list", out var listValue) && listValue is List<object> list)
            {
                if (currentH4 == "Implements")
                {
                    result["implements"] = list;
                }
                continue;
            }

            if (block.TryGetValue("parameters", out var parametersValue) && parametersValue is List<object> parameters)
            {
                if (currentMember is null)
                {
                    if (currentH4 == "Type Parameters")
                    {
                        result["typeParameters"] = parameters;
                    }
                    continue;
                }

                switch (currentH4)
                {
                    case "Parameters":
                        currentMember["parameters"] = parameters;
                        break;
                    case "Returns" when parameters.Count > 0:
                        currentMember["returns"] = parameters[0];
                        break;
                    case "Property Value" or "Field Value" or "Event Type" when parameters.Count > 0
                        && parameters[0] is Dictionary<object, object> valueEntry:
                        currentMember["valueType"] = valueEntry.GetValueOrDefault("type") ?? valueEntry;
                        break;
                    case "Type Parameters":
                        currentMember["typeParameters"] = parameters;
                        break;
                    case "Exceptions":
                        currentMember["exceptions"] = parameters;
                        break;
                }
                continue;
            }
        }

        foreach (var (category, members) in categories)
        {
            result[category] = members;
        }

        return result;
    }
}
