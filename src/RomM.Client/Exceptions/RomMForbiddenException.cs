namespace RomM.Client;

/// <summary>Thrown when the caller is authenticated but not authorized (HTTP 403).</summary>
public sealed class RomMForbiddenException : RomMApiException
{
    public RomMForbiddenException(
        int statusCode,
        string? path,
        string? responseBody,
        string? message = null,
        Exception? innerException = null)
        : base(statusCode, path, responseBody, message ?? "Access forbidden.", innerException)
    {
    }
}
