using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel.HttpClients.Tests;

/// <summary>
/// Source-generated System.Text.Json context for the payload and validation problem types used by the HttpOutcomeMapper tests.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HttpOutcomeMapperTestPayload))]
[JsonSerializable(typeof(ContractValidationProblemDto))]
internal sealed partial class HttpOutcomeMapperTestsJsonContext : JsonSerializerContext;
