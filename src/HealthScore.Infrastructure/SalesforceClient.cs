using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HealthScore.Application;
using Microsoft.Extensions.Options;

namespace HealthScore.Infrastructure;

public sealed class SalesforceClient(HttpClient httpClient, IOptions<SalesforceOptions> options) : ISalesforceClient
{
    private readonly SalesforceOptions _options = options.Value;
    private readonly SemaphoreSlim _authenticationLock = new(1, 1);
    private string? _accessToken;
    private string? _instanceUrl;

    public async IAsyncEnumerable<JsonElement> QueryAsync(
        string soql,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var nextUrl = $"{_instanceUrl}/services/data/{_options.ApiVersion}/query?q={Uri.EscapeDataString(soql)}";

        while (nextUrl is not null)
        {
            using var response = await SendWithTokenRefreshAsync(nextUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));

            foreach (var record in document.RootElement.GetProperty("records").EnumerateArray())
            {
                yield return record.Clone();
            }

            nextUrl = document.RootElement.TryGetProperty("nextRecordsUrl", out var next)
                ? _instanceUrl + next.GetString()
                : null;
        }
    }

    private async Task<HttpResponseMessage> SendWithTokenRefreshAsync(string url, CancellationToken cancellationToken)
    {
        var response = await SendAsync(url, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        _accessToken = null;
        await EnsureAuthenticatedAsync(cancellationToken);
        return await SendAsync(url, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null)
        {
            return;
        }

        await _authenticationLock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null)
            {
                return;
            }

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            });
            using var response = await httpClient.PostAsync(
                $"{_options.LoginUrl.TrimEnd('/')}/services/oauth2/token",
                content,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            _accessToken = document.RootElement.GetProperty("access_token").GetString();
            _instanceUrl = document.RootElement.GetProperty("instance_url").GetString();
        }
        finally
        {
            _authenticationLock.Release();
        }
    }
}
