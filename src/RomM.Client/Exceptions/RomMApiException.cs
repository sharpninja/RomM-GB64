namespace RomM.Client;

/// <summary>Base exception for unsuccessful RomM HTTP responses.</summary>
public class RomMApiException : Exception
{
    public RomMApiException(
        int statusCode,
        string? path,
        string? responseBody,
        string? message = null,
        Exception? innerException = null)
        : base(message ?? BuildDefaultMessage(statusCode, path), innerException)
    {
        StatusCode = statusCode;
        Path = path;
        ResponseBody = responseBody;
    }

    public int StatusCode { get; }

    public string? Path { get; }

    public string? ResponseBody { get; }

    private static string BuildDefaultMessage(int statusCode, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return $"RomM API request failed with status {statusCode}.";
        }

        return $"RomM API request to '{path}' failed with status {statusCode}.";
    }
}
