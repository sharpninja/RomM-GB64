using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RomM.Client.Auth;

namespace RomM.Client;

public interface IRomMClient : IAsyncDisposable
{
    Uri BaseAddress { get; }
    IRomMTransport Transport { get; }
    IRomMSystemClient System { get; }
    IRomMPlatformsClient Platforms { get; }
    IRomMRomsClient Roms { get; }
    IRomMTasksClient Tasks { get; }
}

public sealed class RomMClient : IRomMClient
{
    private readonly RomMTransport _transport;
    private readonly bool _ownsTransport;

    private RomMClient(RomMTransport transport, bool ownsTransport)
    {
        _transport = transport;
        _ownsTransport = ownsTransport;
        System = new RomMSystemClient(_transport);
        Platforms = new RomMPlatformsClient(_transport);
        Roms = new RomMRomsClient(_transport);
        Tasks = new RomMTasksClient(_transport);
    }

    public Uri BaseAddress => _transport.BaseAddress;
    public IRomMTransport Transport => _transport;
    public IRomMSystemClient System { get; }
    public IRomMPlatformsClient Platforms { get; }
    public IRomMRomsClient Roms { get; }
    public IRomMTasksClient Tasks { get; }

    public static RomMClient Create(RomMClientOptions options, HttpMessageHandler? innerHandler = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        HttpMessageHandler pipeline = innerHandler ?? new HttpClientHandler();
        if (options.Auth is not null)
        {
            pipeline = new RomMAuthHandler(options.Auth) { InnerHandler = pipeline };
        }

        var http = new HttpClient(pipeline)
        {
            BaseAddress = options.BaseAddress,
            Timeout = options.Timeout,
        };
        if (!string.IsNullOrWhiteSpace(options.UserAgent))
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        }

        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var transport = new RomMTransport(http, ownsClient: true);
        return new RomMClient(transport, ownsTransport: true);
    }

    public static RomMClient Create(Uri baseAddress, RomMAuth? auth = null, HttpMessageHandler? handler = null) =>
        Create(new RomMClientOptions { BaseAddress = baseAddress, Auth = auth }, handler);

    public ValueTask DisposeAsync()
    {
        if (_ownsTransport)
        {
            return _transport.DisposeAsync();
        }

        return ValueTask.CompletedTask;
    }
}

public static class RomMServiceCollectionExtensions
{
    public static IServiceCollection AddRomMClient(
        this IServiceCollection services,
        Action<RomMClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<RomMClientOptions>().Configure(configure);
        services.AddHttpClient("RomM.Client");
        services.AddSingleton<IRomMClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RomMClientOptions>>().Value;
            opts.Validate();
            return RomMClient.Create(opts);
        });
        return services;
    }
}
