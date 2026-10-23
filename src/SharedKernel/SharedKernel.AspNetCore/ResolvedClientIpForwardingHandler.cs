using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;

namespace PandaBear.AspNetCore;

internal sealed class ResolvedClientIpForwardingHandler(IHttpContextAccessor contextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove(ForwardedHeadersDefaults.XForwardedForHeaderName);
        if (contextAccessor.HttpContext?.Connection.RemoteIpAddress is { } address)
        {
            var resolvedAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            request.Headers.Add(ForwardedHeadersDefaults.XForwardedForHeaderName, resolvedAddress.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
