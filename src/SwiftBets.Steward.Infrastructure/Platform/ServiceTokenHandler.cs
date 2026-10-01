using System.Net.Http.Headers;
using SwiftBets.BuildingBlocks.Web;

namespace SwiftBets.Steward.Infrastructure.Platform;

/// <summary>Attaches Steward's own Service token to every call it makes into the platform.</summary>
public sealed class ServiceTokenHandler(ClientCredentialsTokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokens.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // The issuer restarted or rotated its key; the next call fetches a fresh token.
            tokens.Invalidate(token);
        }

        return response;
    }
}
