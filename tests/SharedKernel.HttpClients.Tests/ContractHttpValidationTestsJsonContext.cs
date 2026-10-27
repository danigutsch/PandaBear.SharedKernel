using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaBear.HttpClients.Tests;

/// <summary>Source-generated JSON serialization context, using web defaults, for the validation problem DTO the contract validation tests produce.</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ContractValidationProblemDto))]
internal sealed partial class ContractHttpValidationTestsJsonContext : JsonSerializerContext;
