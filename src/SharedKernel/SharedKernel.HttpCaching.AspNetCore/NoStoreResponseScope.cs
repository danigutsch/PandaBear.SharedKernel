using Microsoft.AspNetCore.Http;

namespace PandaBear.HttpCaching.AspNetCore;

/// <summary>Path scope whose responses are marked as not storable by caches.</summary>
/// <param name="PathPrefix">Request path prefix that selects the responses in scope.</param>
internal sealed record NoStoreResponseScope(PathString PathPrefix);
