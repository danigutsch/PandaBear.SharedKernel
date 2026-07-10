using System.Text.Json;
using System.Text.Json.Serialization;
using SharedKernel.HttpClients;

namespace SharedKernel.Branding;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(BrandingSettingsDto))]
[JsonSerializable(typeof(ContractValidationProblemDto))]
internal sealed partial class BrandingApiClientJsonContext : JsonSerializerContext;
