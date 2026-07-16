using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

/// <summary>Nuke orchestration for RomM.Client packages.</summary>
partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (CI)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    [Parameter("Package version for NuGet pack (defaults to 1.0.0 from project files)")]
    readonly string PackageVersion = string.Empty;

    public AbsolutePath SourceDirectory => RootDirectory / "src";
    public AbsolutePath TestsDirectory => RootDirectory / "tests";
    public AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    public AbsolutePath ClientSolution => RootDirectory / "RomM.Client.slnx";

    AbsolutePath PackageOutputDirectory => ArtifactsDirectory / "nupkg";

    Target Clean => _ => _
        .Executes(() =>
        {
            if (Directory.Exists(ArtifactsDirectory))
            {
                Directory.Delete(ArtifactsDirectory, recursive: true);
            }

            Directory.CreateDirectory(ArtifactsDirectory);
            Directory.CreateDirectory(PackageOutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(_ => _
                .SetProjectFile(ClientSolution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(_ => _
                .SetProjectFile(ClientSolution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(TestsDirectory / "RomM.Client.Tests" / "RomM.Client.Tests.csproj")
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target PackNuGet => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            CleanPackageOutput(PackageOutputDirectory);
            var version = string.IsNullOrWhiteSpace(PackageVersion) ? null : PackageVersion.Trim();
            var projects = new[]
            {
                SourceDirectory / "RomM.Client" / "RomM.Client.csproj",
                SourceDirectory / "RomM.Client.Csdb" / "RomM.Client.Csdb.csproj",
            };

            foreach (var project in projects)
            {
                var settings = new DotNetPackSettings()
                    .SetProject(project)
                    .SetConfiguration("Release")
                    .SetOutputDirectory(PackageOutputDirectory)
                    .EnableNoRestore();

                if (version is not null)
                {
                    settings = settings
                        .SetProperty("PackageVersion", version)
                        .SetProperty("Version", version)
                        .SetProperty("InformationalVersion", version);
                }

                Log.Information("Packing {Project}", project);
                DotNetPack(_ => settings);
            }
        });

    /// <summary>Primary env name (uppercase). Also accepts nuget_api_key for operator convenience.</summary>
    public const string NuGetApiKeyEnvironmentVariable = "NUGET_API_KEY";

    public const string NuGetOrgSource = "https://api.nuget.org/v3/index.json";

    Target PublishNuGet => _ => _
        .DependsOn(PackNuGet)
        .Executes(() =>
        {
            var apiKey = ResolveNuGetApiKey(Environment.GetEnvironmentVariable);
            var packages = GetNuGetPackagesToPublish(PackageOutputDirectory);
            if (packages.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No NuGet packages found under '{PackageOutputDirectory}'. Run PackNuGet first.");
            }

            foreach (var package in packages)
            {
                Log.Information("Publishing {Package} to {Source}", package.Name, NuGetOrgSource);
                DotNetNuGetPush(_ => _
                    .SetTargetPath(package)
                    .SetSource(NuGetOrgSource)
                    .SetApiKey(apiKey)
                    .EnableSkipDuplicate());
            }
        });

    internal static string ResolveNuGetApiKey(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        // Prefer NUGET_API_KEY; also accept nuget_api_key as requested by operators.
        var apiKey = getEnvironmentVariable(NuGetApiKeyEnvironmentVariable)
            ?? getEnvironmentVariable("nuget_api_key")
            ?? getEnvironmentVariable("NUGET_TOKEN");

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("$(", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Set NUGET_API_KEY (or nuget_api_key) in the environment before running PublishNuGet.");
        }

        return apiKey;
    }

    internal static IReadOnlyList<AbsolutePath> GetNuGetPackagesToPublish(AbsolutePath packageDirectory)
    {
        if (!Directory.Exists(packageDirectory))
        {
            return [];
        }

        return Directory.GetFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (AbsolutePath)path)
            .ToArray();
    }

    internal static void CleanPackageOutput(AbsolutePath packageDirectory)
    {
        Directory.CreateDirectory(packageDirectory);
        foreach (var package in Directory.GetFiles(packageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly))
        {
            File.Delete(package);
        }
    }
}
