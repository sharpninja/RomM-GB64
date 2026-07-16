using System.Net.Http.Headers;
using RomM.Client.Http;
using RomM.Client.Json;

namespace RomM.Client;

/// <summary>Low-level authenticated HTTP transport for any /api path.</summary>
public interface IRomMTransport
{
    Uri BaseAddress { get; }
    Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUrl,
        HttpContent? content = null,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead,
        CancellationToken cancellationToken = default);
    Task<T> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken = default);
    Task<T> SendJsonAsync<T>(HttpMethod method, string relativeUrl, HttpContent? content, CancellationToken cancellationToken = default);
}

public sealed class RomMTransport : IRomMTransport, IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public RomMTransport(HttpClient http, bool ownsClient = false)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsClient = ownsClient;
        if (_http.BaseAddress is null)
        {
            throw new ArgumentException("HttpClient.BaseAddress is required.", nameof(http));
        }
    }

    public Uri BaseAddress => _http.BaseAddress!;

    public async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUrl,
        HttpContent? content = null,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, relativeUrl.TrimStart('/'));
        request.Content = content;
        var response = await _http.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
        await RomMHttp.EnsureSuccessOrThrowAsync(response, cancellationToken).ConfigureAwait(false);
        return response;
    }

    public async Task<T> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, relativeUrl, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return await RomMJson.ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SendJsonAsync<T>(
        HttpMethod method,
        string relativeUrl,
        HttpContent? content,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(method, relativeUrl, content, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return await RomMJson.ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsClient)
        {
            _http.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
