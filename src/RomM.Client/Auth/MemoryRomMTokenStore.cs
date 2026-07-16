namespace RomM.Client.Auth;

/// <summary>In-memory <see cref="IRomMTokenStore"/> suitable for single-process clients and tests.</summary>
public sealed class MemoryRomMTokenStore : IRomMTokenStore
{
    private readonly object _gate = new();
    private RomMToken? _token;

    public Task<RomMToken?> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult(_token);
        }
    }

    public Task SetAsync(RomMToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _token = token;
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _token = null;
        }

        return Task.CompletedTask;
    }
}
