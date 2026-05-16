using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace MedAssist.AI.Extensions;

public static class SharedConfigurationExtensions
{
    private const string _configPathEnvVar = "MEDASSIST_CONFIG_PATH";
    private const string _sharedFileName = "appsettings.shared.json";
    private const string _envFilePattern = "appsettings.{0}.json";

    public static IConfigurationBuilder AddSharedConfiguration(this IConfigurationBuilder builder)
    {
        var configDir = ResolveSharedConfigDir();
        if (configDir is null)
        {
            return builder;
        }

        // Insert at 0 = lowest priority; environment variables and secrets still win
        builder.Sources.Insert(0, new JsonConfigurationSource
        {
            Path = Path.Combine(configDir, _sharedFileName),
            Optional = true,
            ReloadOnChange = false
        });

        // Environment-specific override (e.g. appsettings.Development.json), slightly higher priority
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Production";

        builder.Sources.Insert(1, new JsonConfigurationSource
        {
            Path = Path.Combine(configDir, string.Format(_envFilePattern, environment)),
            Optional = true,
            ReloadOnChange = false
        });

        return builder;
    }

    private static string? ResolveSharedConfigDir()
    {
        // 1. Explicit override — used by Docker and CI
        var explicitDir = Environment.GetEnvironmentVariable(_configPathEnvVar);
        if (explicitDir is not null)
        {
            return explicitDir;
        }

        // 2. Walk up from the app's base directory to find the solution root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateFiles("*.sln").Any())
            {
                return Path.Combine(dir.FullName, "config");
            }

            dir = dir.Parent;
        }

        return null;
    }
}
