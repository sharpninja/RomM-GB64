using System.Net;

namespace RomM.Client.Tests;

/// <summary>Records outbound requests and returns scripted responses for auth tests.</summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>> _responder;
    private int _callIndex;

    public RecordingHttpMessageHandler(
        Func<HttpRequestMessage, int, CancellationToken, Task<HttpResponseMessage>>? responder = null)
    {
        _responder = responder ?? ((_, _, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            }));
    }

    public List<RecordedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var index = Interlocked.Increment(ref _callIndex) - 1;
        var auth = request.Headers.Authorization;
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri,
            auth?.Scheme,
            auth?.Parameter,
            body,
            request.Content?.Headers.ContentType?.MediaType));

        return await _responder(request, index, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri? RequestUri,
    string? AuthScheme,
    string? AuthParameter,
    string? Body,
    string? ContentType);

/// <summary>Fixed-status inner handler for exception-mapping tests.</summary>
internal sealed class FixedStatusHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public FixedStatusHandler(HttpStatusCode status, string body = "")
    {
        _status = status;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body),
            RequestMessage = request,
        };
        return Task.FromResult(response);
    }
}
