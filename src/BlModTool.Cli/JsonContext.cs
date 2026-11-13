using System.Text.Json.Serialization;

namespace BlModTool.Cli;

[JsonSerializable(typeof(ConversionReport))]
[JsonSerializable(typeof(ConversionCounts))]
[JsonSerializable(typeof(SourcePaths))]
[JsonSerializable(typeof(ConversionSkip))]
[JsonSerializable(typeof(WarbandPortOptions))]
internal partial class WarbandJsonContext : JsonSerializerContext
{
}