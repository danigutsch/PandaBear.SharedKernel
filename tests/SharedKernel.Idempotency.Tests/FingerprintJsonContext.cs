using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.Idempotency.Tests;

[JsonSerializable(typeof(JsonElement))]
internal sealed partial class FingerprintJsonContext : JsonSerializerContext;
