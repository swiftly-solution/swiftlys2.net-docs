using System.Text.Json.Nodes;

namespace DotnetYamlApiCompressor;

public static class ApiPageParser
{
    public static JsonObject Parse(JsonArray body)
    {
        var blocks = new List<JsonObject>();
        while (body.Count > 0)
        {
            var node = body[0];
            body.RemoveAt(0);
            if (node is JsonObject obj)
            {
                blocks.Add(obj);
            }
        }

        var leadingBlocks = new JsonArray();
        var sections = new JsonArray();
        JsonArray? currentSectionBlocks = null;
        JsonArray? currentSectionEntries = null;
        JsonArray? currentEntryBlocks = null;

        foreach (var block in blocks)
        {
            var headingKey = block.ContainsKey("h2") ? "h2" : block.ContainsKey("h3") ? "h3" : null;
            if (headingKey is not null)
            {
                currentSectionBlocks = new JsonArray();
                currentSectionEntries = new JsonArray();
                sections.Add(new JsonObject
                {
                    ["level"] = headingKey,
                    ["heading"] = block,
                    ["blocks"] = currentSectionBlocks,
                    ["entries"] = currentSectionEntries,
                });
                currentEntryBlocks = null;
                continue;
            }

            if (block.ContainsKey("api1") || block.ContainsKey("api3"))
            {
                currentEntryBlocks = new JsonArray();
                var entry = new JsonObject
                {
                    ["header"] = block,
                    ["blocks"] = currentEntryBlocks,
                };

                if (currentSectionEntries is not null)
                {
                    currentSectionEntries.Add(entry);
                }
                else
                {
                    leadingBlocks.Add(entry);
                }
                continue;
            }

            if (currentEntryBlocks is not null)
            {
                currentEntryBlocks.Add(block);
            }
            else if (currentSectionBlocks is not null)
            {
                currentSectionBlocks.Add(block);
            }
            else
            {
                leadingBlocks.Add(block);
            }
        }

        return new JsonObject
        {
            ["leadingBlocks"] = leadingBlocks,
            ["sections"] = sections,
        };
    }
}
