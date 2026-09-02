using System.Text.Json.Nodes;

namespace DotnetYamlApiCompressor;

public static class JsonNodeConverter
{
    public static JsonNode? ToJsonNode(object? value) => value switch
    {
        null => null,
        IDictionary<object, object> map => ToJsonObject(map),
        List<object> list => ToJsonArray(list),
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        byte by => JsonValue.Create((int)by),
        sbyte sb => JsonValue.Create((int)sb),
        short sh => JsonValue.Create((int)sh),
        ushort ush => JsonValue.Create((int)ush),
        int i => JsonValue.Create(i),
        uint ui => ui <= int.MaxValue ? JsonValue.Create((int)ui) : JsonValue.Create((long)ui),
        long l => l is >= int.MinValue and <= int.MaxValue ? JsonValue.Create((int)l) : JsonValue.Create(l),
        ulong ul => ul <= int.MaxValue ? JsonValue.Create((int)ul) : ul <= long.MaxValue ? JsonValue.Create((long)ul) : JsonValue.Create(ul),
        float f => JsonValue.Create(f),
        double d => JsonValue.Create(d),
        decimal dec => JsonValue.Create(dec),
        DateTime dt => JsonValue.Create(dt.ToString("o")),
        _ => JsonValue.Create(value.ToString()),
    };

    private static JsonObject ToJsonObject(IDictionary<object, object> map)
    {
        var obj = new JsonObject();
        foreach (var pair in map)
        {
            obj[pair.Key.ToString()!] = ToJsonNode(pair.Value);
        }
        return obj;
    }

    private static JsonArray ToJsonArray(List<object> list)
    {
        var array = new JsonArray();
        foreach (var item in list)
        {
            array.Add(ToJsonNode(item));
        }
        return array;
    }
}
