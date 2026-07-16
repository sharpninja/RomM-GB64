using Microsoft.Extensions.DependencyInjection;
using RomM.Client;

namespace RomM.Client.Csdb;

public interface ICsdbRomMWorkflow
{
    Task<CsdbIngestAndScanResult> IngestSelectedAsync(
        IReadOnlyList<CsdbSelection> selections,
        bool scanAfterIngest = true,
        CsdbIngestOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class CsdbRomMWorkflow : ICsdbRomMWorkflow
{
    private readonly ICsdbLibraryWriter _writer;
    private readonly IRomMClient? _romm;

    public CsdbRomMWorkflow(ICsdbLibraryWriter writer, IRomMClient? romm = null)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _romm = romm;
    }

    public async Task<CsdbIngestAndScanResult> IngestSelectedAsync(
        IReadOnlyList<CsdbSelection> selections,
        bool scanAfterIngest = true,
        CsdbIngestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var ingest = await _writer.IngestAsync(selections, options, cancellationToken).ConfigureAwait(false);
        var anyOk = ingest.Items.Any(i => i.Status is "ok" or "skipped");
        var scanRequested = scanAfterIngest && anyOk && _romm is not null;
        var scanCompleted = false;
        if (scanRequested)
        {
            await _romm!.Tasks.ScanLibraryAsync(cancellationToken).ConfigureAwait(false);
            scanCompleted = true;
        }

        return new CsdbIngestAndScanResult(ingest, scanRequested, scanCompleted);
    }
}

public static class RomMCsdbServiceCollectionExtensions
{
    public static IServiceCollection AddRomMCsdb(
        this IServiceCollection services,
        Action<CsdbLibraryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        var opts = new CsdbLibraryOptions { LibraryRomsRoot = Path.GetTempPath() };
        configure(opts);
        opts.Validate();
        services.AddSingleton(opts);
        services.AddSingleton<ICsdbClient>(sp => CsdbClient.Create(sp.GetRequiredService<CsdbLibraryOptions>()));
        services.AddSingleton<ICsdbLibraryWriter, CsdbLibraryWriter>();
        services.AddSingleton<ICsdbRomMWorkflow>(sp =>
            new CsdbRomMWorkflow(
                sp.GetRequiredService<ICsdbLibraryWriter>(),
                sp.GetService<IRomMClient>()));
        return services;
    }
}
