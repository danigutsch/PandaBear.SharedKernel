using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpRequests.Sample;

[JsonSerializable(typeof(JsonElement))]
internal sealed partial class RequestJsonContext : JsonSerializerContext;
