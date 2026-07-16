namespace RomM.Client.Auth;

/// <summary>Optional store for OAuth access and refresh tokens.</summary>
public interface IRomMTokenStore
{
    Task<RomMToken?> GetAsync(CancellationToken cancellationToken = default);

    Task SetAsync(RomMToken token, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
