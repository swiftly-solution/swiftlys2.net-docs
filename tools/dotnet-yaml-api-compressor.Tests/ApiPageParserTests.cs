using YamlDotNet.Serialization;
using System.Text.Json.Nodes;
using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class ApiPageParserTests
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    private static JsonArray ParseBody(string yaml)
    {
        var raw = Deserializer.Deserialize<object>(yaml);
        var root = (JsonObject)JsonNodeConverter.ToJsonNode(raw)!;
        return (JsonArray)root["body"]!;
    }

    [Fact]
    public void GroupsMembersIntoSectionsAndEntries()
    {
        var body = ParseBody("""
            title: Interface ICommandContext
            body:
            - api1: Interface ICommandContext
              id: SwiftlyS2_Shared_Commands_ICommandContext
              metadata:
                uid: SwiftlyS2.Shared.Commands.ICommandContext
                commentId: T:SwiftlyS2.Shared.Commands.ICommandContext
            - facts:
              - name: Namespace
                value:
                  text: SwiftlyS2.Shared.Commands
                  url: SwiftlyS2.Shared.Commands.html
              - name: Assembly
                value: SwiftlyS2.CS2.dll
            - code: public interface ICommandContext
            - h2: Properties
            - api3: Args
              id: SwiftlyS2_Shared_Commands_ICommandContext_Args
              metadata:
                uid: SwiftlyS2.Shared.Commands.ICommandContext.Args
                commentId: P:SwiftlyS2.Shared.Commands.ICommandContext.Args
            - markdown: Gets the array of arguments passed with the command.
            - code: string[] Args { get; }
            - h2: Methods
            - api3: Reply(string)
              id: SwiftlyS2_Shared_Commands_ICommandContext_Reply_System_String_
              metadata:
                uid: SwiftlyS2.Shared.Commands.ICommandContext.Reply(System.String)
                commentId: M:SwiftlyS2.Shared.Commands.ICommandContext.Reply(System.String)
            - markdown: Sends a reply message to the command sender.
            - code: void Reply(string message)
            - h4: Parameters
            - parameters:
              - name: message
                type:
                - text: string
                  url: https://learn.microsoft.com/dotnet/api/system.string
                description: The message to send as a reply.
            """);

        var parsed = ApiPageParser.Parse(body);

        // The page's own api1 header ends up as the single entry in
        // leadingBlocks, with its facts/code nested inside ITS OWN blocks
        // array — the same rule every api3 member entry follows. This is
        // correct: verified by hand-tracing the algorithm against this exact
        // fixture. Do not "fix" this into 3 separate top-level items.
        var leading = parsed["leadingBlocks"]!.AsArray();
        Assert.Single(leading);
        var header = leading[0]!.AsObject();
        Assert.Equal("Interface ICommandContext", header["header"]!["api1"]!.GetValue<string>());
        var headerBlocks = header["blocks"]!.AsArray();
        Assert.Equal(2, headerBlocks.Count);
        Assert.True(headerBlocks[0]!.AsObject().ContainsKey("facts"));
        Assert.Equal("public interface ICommandContext", headerBlocks[1]!["code"]!.GetValue<string>());

        var sections = parsed["sections"]!.AsArray();
        Assert.Equal(2, sections.Count);

        var properties = sections[0]!.AsObject();
        Assert.Equal("h2", properties["level"]!.GetValue<string>());
        Assert.Equal("Properties", properties["heading"]!["h2"]!.GetValue<string>());
        var propertyEntries = properties["entries"]!.AsArray();
        Assert.Single(propertyEntries);
        var argsEntry = propertyEntries[0]!.AsObject();
        Assert.Equal("Args", argsEntry["header"]!["api3"]!.GetValue<string>());
        var argsBlocks = argsEntry["blocks"]!.AsArray();
        Assert.Equal(2, argsBlocks.Count);
        Assert.Equal("Gets the array of arguments passed with the command.", argsBlocks[0]!["markdown"]!.GetValue<string>());
        Assert.Equal("string[] Args { get; }", argsBlocks[1]!["code"]!.GetValue<string>());

        var methods = sections[1]!.AsObject();
        Assert.Empty(properties["blocks"]!.AsArray());
        Assert.Empty(methods["blocks"]!.AsArray());
        Assert.Equal("Methods", methods["heading"]!["h2"]!.GetValue<string>());
        var methodEntries = methods["entries"]!.AsArray();
        Assert.Single(methodEntries);
        var replyEntry = methodEntries[0]!.AsObject();
        Assert.Equal("Reply(string)", replyEntry["header"]!["api3"]!.GetValue<string>());
        var replyBlocks = replyEntry["blocks"]!.AsArray();
        Assert.Equal(4, replyBlocks.Count);
        Assert.True(replyBlocks[2]!.AsObject().ContainsKey("h4"));
        Assert.True(replyBlocks[3]!.AsObject().ContainsKey("parameters"));
    }

    [Fact]
    public void NamespacePageGroupsChildTypeListsByH3Category()
    {
        var body = ParseBody("""
            title: Namespace My.Ns
            body:
            - api1: Namespace My.Ns
              metadata:
                uid: My.Ns
            - h3: Classes
            - parameters:
              - type:
                  text: Widget
                  url: My.Ns.Widget.html
            - h3: Interfaces
            - parameters:
              - type:
                  text: IThing
                  url: My.Ns.IThing.html
            """);

        var parsed = ApiPageParser.Parse(body);

        var sections = parsed["sections"]!.AsArray();
        Assert.Equal(2, sections.Count);

        var classes = sections[0]!.AsObject();
        Assert.Equal("h3", classes["level"]!.GetValue<string>());
        Assert.Equal("Classes", classes["heading"]!["h3"]!.GetValue<string>());
        // A namespace category listing has no api3 header of its own, so its
        // "parameters" block lands in the SECTION's own blocks array, not in
        // an entry — entries stays empty for namespace category sections.
        Assert.Empty(classes["entries"]!.AsArray());
        var classesBlocks = classes["blocks"]!.AsArray();
        Assert.Single(classesBlocks);
        Assert.True(classesBlocks[0]!.AsObject().ContainsKey("parameters"));

        var interfaces = sections[1]!.AsObject();
        Assert.Equal("Interfaces", interfaces["heading"]!["h3"]!.GetValue<string>());
        Assert.Single(interfaces["blocks"]!.AsArray());
    }
}
