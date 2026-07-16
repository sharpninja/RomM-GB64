using System.Net;

namespace RomM.Client.Http;

/// <summary>Shared HTTP pipeline helpers for RomM clients.</summary>
public static class RomMHttp
{
    /// <summary>
    /// Throws a typed <see cref="RomMApiException"/> (or subclass) when the response is not successful.
    /// Does not include caller credentials in exception messages.
    /// </summary>
    public static async Task EnsureSuccessOrThrowAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var path = response.RequestMessage?.RequestUri is { } uri
            ? uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString
            : null;

        string? body = null;
        if (response.Content is not null)
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        var statusCode = (int)response.StatusCode;
        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new RomMAuthException(statusCode, path, body),
            HttpStatusCode.Forbidden => new RomMForbiddenException(statusCode, path, body),
            _ => new RomMApiException(statusCode, path, body),
        };
    }
}
