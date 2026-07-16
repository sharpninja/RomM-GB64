using RomM.Client.Auth;

namespace RomM.Client;

/// <summary>HTTP client options for the RomM API.</summary>
public sealed class RomMClientOptions
{
    public Uri? BaseAddress { get; set; }

    public string UserAgent { get; set; } = "RomM.Client";

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Optional authentication configuration (Client API Token, Basic, or OAuth password).</summary>
    public RomMAuth? Auth { get; set; }

    /// <summary>
    /// Ensures required options are present and normalizes <see cref="BaseAddress"/> so its
    /// absolute URI ends with a trailing slash.
    /// </summary>
    public void Validate()
    {
        if (BaseAddress is null)
        {
            throw new ArgumentException("BaseAddress is required.", nameof(BaseAddress));
        }

        var absolute = BaseAddress.AbsoluteUri;
        if (!absolute.EndsWith('/'))
        {
            BaseAddress = new Uri(absolute + "/", UriKind.Absolute);
        }
    }
}
