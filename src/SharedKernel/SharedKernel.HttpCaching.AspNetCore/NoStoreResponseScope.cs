using Microsoft.AspNetCore.Http;

namespace PandaBear.HttpCaching.AspNetCore;

internal sealed record NoStoreResponseScope(PathString PathPrefix);
