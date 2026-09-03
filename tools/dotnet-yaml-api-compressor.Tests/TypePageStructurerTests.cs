using YamlDotNet.Serialization;
using DotnetYamlApiCompressor;

namespace DotnetYamlApiCompressorTests;

public class TypePageStructurerTests
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithAttemptingUnquotedStringTypeDeserialization()
        .Build();

    private static List<object> ParseBody(string yaml)
    {
        var root = Deserializer.Deserialize<Dictionary<object, object>>(yaml);
        return (List<object>)root["body"];
    }

    private static Dictionary<object, object> M(object o) => (Dictionary<object, object>)o;

    private static List<object> L(object o) => (List<object>)o;

    private static string S(object o) => (string)o;

    [Fact]
    public void SeparatesPropertiesFromMethodsAndCapturesValueTypeAndParameters()
    {
        var body = ParseBody("""
            title: Interface ICommandContext
            body:
            - api1: Interface ICommandContext
              src: https://github.com/swiftly-solution/swiftlys2/blob/master/x.cs#L5
              metadata:
                uid: SwiftlyS2.Shared.Commands.ICommandContext
            - facts:
              - name: Namespace
                value:
                  text: SwiftlyS2.Shared.Commands
            - code: public interface ICommandContext
            - h2: Properties
            - api3: Args
              metadata:
                uid: SwiftlyS2.Shared.Commands.ICommandContext.Args
            - markdown: Gets the array of arguments passed with the command.
            - code: string[] Args { get; }
            - h4: Property Value
            - parameters:
              - type:
                - text: string
                  url: https://learn.microsoft.com/dotnet/api/system.string
                - '['
                - ']'
            - h2: Methods
            - api3: Reply(string)
              src: https://github.com/swiftly-solution/swiftlys2/blob/master/x.cs#L41
              metadata:
                uid: SwiftlyS2.Shared.Commands.ICommandContext.Reply(System.String)
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

        var result = TypePageStructurer.Structure(body);

        Assert.Equal("public interface ICommandContext", S(result["declaration"]));
        Assert.Equal("https://github.com/swiftly-solution/swiftlys2/blob/master/x.cs#L5", S(result["sourceUrl"]));

        var properties = L(result["properties"]);
        Assert.Single(properties);
        var args = M(properties[0]);
        Assert.Equal("Args", S(args["name"]));
        Assert.Equal("SwiftlyS2.Shared.Commands.ICommandContext.Args", S(args["uid"]));
        Assert.Equal("Gets the array of arguments passed with the command.", S(args["summary"]));
        Assert.Equal("string[] Args { get; }", S(args["declaration"]));
        var valueType = L(args["valueType"]);
        Assert.Equal(3, valueType.Count);
        Assert.Equal("string", S(M(valueType[0])["text"]));

        var methods = L(result["methods"]);
        Assert.Single(methods);
        var reply = M(methods[0]);
        Assert.Equal("Reply(string)", S(reply["name"]));
        Assert.Equal("https://github.com/swiftly-solution/swiftlys2/blob/master/x.cs#L41", S(reply["sourceUrl"]));
        var parameters = L(reply["parameters"]);
        Assert.Single(parameters);
        Assert.Equal("message", S(M(parameters[0])["name"]));

        Assert.False(result.ContainsKey("id"));
        Assert.False(result.ContainsKey("facts"));
        Assert.False(args.ContainsKey("id"));
    }

    [Fact]
    public void CapturesInheritanceImplementsAndTypeLevelRemarksExcludingSelfFromChain()
    {
        var body = ParseBody("""
            title: Class TextMenuOption
            body:
            - api1: Class TextMenuOption
              metadata:
                uid: SwiftlyS2.Core.Menus.OptionsBase.TextMenuOption
            - facts:
              - name: Namespace
                value:
                  text: SwiftlyS2.Core.Menus.OptionsBase
            - markdown: Represents a simple text-only menu option without interactive behavior.
            - code: 'public sealed class TextMenuOption : MenuOptionBase, IMenuOption, IDisposable'
            - h4: Inheritance
            - inheritance:
              - text: object
                url: https://learn.microsoft.com/dotnet/api/system.object
              - text: MenuOptionBase
                url: SwiftlyS2.Core.Menus.OptionsBase.MenuOptionBase.html
              - text: TextMenuOption
                url: SwiftlyS2.Core.Menus.OptionsBase.TextMenuOption.html
            - h4: Implements
            - list:
              - text: IMenuOption
                url: SwiftlyS2.Shared.Menus.IMenuOption.html
              - text: IDisposable
                url: https://learn.microsoft.com/dotnet/api/system.idisposable
            - h4: Inherited Members
            - list:
              - text: MenuOptionBase.Dispose()
                url: SwiftlyS2.Core.Menus.OptionsBase.MenuOptionBase.html#Dispose
            - h2: Remarks
            - markdown: 'NOTE: this is a type-level remark, not a member.'
            """);

        var result = TypePageStructurer.Structure(body);

        Assert.Equal("Represents a simple text-only menu option without interactive behavior.", S(result["summary"]));

        var inherits = L(result["inherits"]);
        Assert.Equal(2, inherits.Count);
        Assert.Equal("object", S(M(inherits[0])["text"]));
        Assert.Equal("MenuOptionBase", S(M(inherits[1])["text"]));
        Assert.DoesNotContain(inherits, e => S(M(e)["text"]) == "TextMenuOption");

        var implements = L(result["implements"]);
        Assert.Equal(2, implements.Count);
        Assert.Equal("IMenuOption", S(M(implements[0])["text"]));

        Assert.False(result.ContainsKey("inheritedMembers"));

        Assert.Equal("NOTE: this is a type-level remark, not a member.", S(result["remarks"]));
        Assert.False(result.ContainsKey("methods"));
        Assert.False(result.ContainsKey("properties"));
    }

    [Fact]
    public void CollapsesExcessBlankLinesInSummaryAndDeclaration()
    {
        var body = ParseBody("""
            title: Interface IGameEventService
            body:
            - api1: Interface IGameEventService
              metadata:
                uid: SwiftlyS2.Shared.GameEvents.IGameEventService
            - facts:
              - name: Namespace
                value:
                  text: SwiftlyS2.Shared.GameEvents
            - h2: Methods
            - api3: Fire<T>()
              metadata:
                uid: SwiftlyS2.Shared.GameEvents.IGameEventService.Fire``1
            - markdown: >-
                Fires an event to all players.


                Thread unsafe, use async variant instead for non-main thread context.
            - code: >-
                [ThreadUnsafe]


                void Fire<T>() where T : IGameEvent<T>
            """);

        var result = TypePageStructurer.Structure(body);
        var method = M(L(result["methods"])[0]);

        Assert.Equal(
            "Fires an event to all players. Thread unsafe, use async variant instead for non-main thread context.",
            S(method["summary"]));
        Assert.Equal("[ThreadUnsafe] void Fire<T>() where T : IGameEvent<T>", S(method["declaration"]));
    }

    [Fact]
    public void ReplacesEmbeddedTabsSoThePlainYamlScalarStaysValid()
    {
        var body = ParseBody("""
            title: Class Widget
            body:
            - api1: Class Widget
              metadata:
                uid: My.Ns.Widget
            - facts:
              - name: Namespace
                value:
                  text: My.Ns
            - markdown: "line one\tthis has a tab\tand another"
            """);

        var result = TypePageStructurer.Structure(body);

        Assert.Equal("line one this has a tab and another", S(result["summary"]));
    }

    [Fact]
    public void FlattensSingleEntryReturnsBlockAndSeparatesOperatorsFromMethods()
    {
        var body = ParseBody("""
            title: Struct FriendsGroupID_t
            body:
            - api1: Struct FriendsGroupID_t
              metadata:
                uid: SwiftlyS2.Shared.SteamAPI.FriendsGroupID_t
            - facts:
              - name: Namespace
                value:
                  text: SwiftlyS2.Shared.SteamAPI
            - code: public struct FriendsGroupID_t
            - h2: Methods
            - api3: CompareTo(FriendsGroupID_t)
              metadata:
                uid: SwiftlyS2.Shared.SteamAPI.FriendsGroupID_t.CompareTo(SwiftlyS2.Shared.SteamAPI.FriendsGroupID_t)
            - code: public int CompareTo(FriendsGroupID_t other)
            - h4: Parameters
            - parameters:
              - name: other
                type:
                - text: FriendsGroupID_t
                  url: SwiftlyS2.Shared.SteamAPI.FriendsGroupID_t.html
            - h4: Returns
            - parameters:
              - type:
                - text: int
                  url: https://learn.microsoft.com/dotnet/api/system.int32
                description: A value that indicates the relative order.
            - h2: Operators
            - api3: operator ==(FriendsGroupID_t, FriendsGroupID_t)
              metadata:
                uid: SwiftlyS2.Shared.SteamAPI.FriendsGroupID_t.op_Equality(SwiftlyS2.Shared.SteamAPI.FriendsGroupID_t,SwiftlyS2.Shared.SteamAPI.FriendsGroupID_t)
            - code: public static bool operator ==(FriendsGroupID_t x, FriendsGroupID_t y)
            """);

        var result = TypePageStructurer.Structure(body);

        var methods = L(result["methods"]);
        Assert.Single(methods);
        var compareTo = M(methods[0]);
        var returns = M(compareTo["returns"]);
        Assert.Equal("A value that indicates the relative order.", S(returns["description"]));
        var returnsType = L(returns["type"]);
        Assert.Equal("int", S(M(returnsType[0])["text"]));

        var operators = L(result["operators"]);
        Assert.Single(operators);
        Assert.Equal("operator ==(FriendsGroupID_t, FriendsGroupID_t)", S(M(operators[0])["name"]));

        Assert.DoesNotContain(methods, m => S(M(m)["name"]).StartsWith("operator"));
    }
}
