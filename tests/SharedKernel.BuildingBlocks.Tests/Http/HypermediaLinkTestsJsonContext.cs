using System.Text.Json.Serialization;

namespace SharedKernel.BuildingBlocks.Tests;

/// <summary>Source-generated JSON metadata for the hypermedia link wire-shape tests.</summary>
[JsonSerializable(typeof(HypermediaLink))]
internal sealed partial class HypermediaLinkTestsJsonContext : JsonSerializerContext;
