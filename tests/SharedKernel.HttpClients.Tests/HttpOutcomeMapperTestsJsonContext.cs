using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.HttpClients.Tests;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HttpOutcomeMapperTestPayload))]
[JsonSerializable(typeof(ContractValidationProblemDto))]
internal sealed partial class HttpOutcomeMapperTestsJsonContext : JsonSerializerContext;
