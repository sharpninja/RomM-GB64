namespace RomM.Client;

/// <summary>Thrown when authentication fails (HTTP 401) or OAuth token grant/refresh fails.</summary>
public sealed class RomMAuthException : RomMApiException
{
    public RomMAuthException(
        int statusCode,
        string? path,
        string? responseBody,
        string? message = null,
        Exception? innerException = null)
        : base(statusCode, path, responseBody, message ?? "Authentication failed.", innerException)
    {
    }
}
