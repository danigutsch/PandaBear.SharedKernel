using System.Text.Json.Serialization;

namespace Aspire.Hosting;

/// <summary>Source-generated serialization of the Umami requests and answers that provisioning uses, so it needs no reflection.</summary>
[JsonSerializable(typeof(UmamiLoginRequest))]
[JsonSerializable(typeof(UmamiLoginResponse))]
[JsonSerializable(typeof(UmamiCreateWebsiteRequest))]
[JsonSerializable(typeof(UmamiWebsiteReply))]
internal sealed partial class UmamiApiJsonContext : JsonSerializerContext;
