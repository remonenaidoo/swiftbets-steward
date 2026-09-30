using System.Net.Http.Headers;
using SwiftBets.BuildingBlocks.Web;

namespace SwiftBets.Steward.Infrastructure.Platform;

/// <summary>Attaches Steward's own Service token to every call it makes into the platform.</summary>
public sealed class ServiceTokenHandler(ClientCredentialsTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(cancellationToken));
        return await base.SendAsync(request, cancellationToken);
    }
}
