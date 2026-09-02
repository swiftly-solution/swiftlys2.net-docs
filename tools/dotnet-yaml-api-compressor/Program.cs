using System.Text.Json;
using DotnetYamlApiCompressor;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: dotnet-yaml-api-compressor <inputDir> <outputFile> <branchLabel>");
    return 1;
}

var inputDir = args[0];
var outputFile = args[1];
var branchLabel = args[2];

if (!Directory.Exists(inputDir))
{
    Console.Error.WriteLine($"Input directory does not exist: {inputDir}");
    return 1;
}

var pages = YamlDocumentLoader.LoadDirectory(inputDir);

if (!pages.Any(p => p.ContainsKey("uid") && p.ContainsKey("type")))
{
    Console.Error.WriteLine($"Input directory exists but contains no usable docfx metadata items (checked for *.yml/*.yaml files other than toc.yml): {inputDir}");
    return 1;
}

var root = ApiTreeBuilder.Build(pages, branchLabel);

var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputFile));
if (!string.IsNullOrEmpty(outputDir))
{
    Directory.CreateDirectory(outputDir);
}

File.WriteAllText(outputFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote {outputFile}");
return 0;
