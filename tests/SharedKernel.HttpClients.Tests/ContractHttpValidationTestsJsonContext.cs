using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel.HttpClients.Tests;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ContractValidationProblemDto))]
internal sealed partial class ContractHttpValidationTestsJsonContext : JsonSerializerContext;
