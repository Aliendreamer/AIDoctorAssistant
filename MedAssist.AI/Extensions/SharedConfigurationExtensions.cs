using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace MedAssist.AI.Extensions;

public static class SharedConfigurationExtensions
{
    private const string _configPathEnvVar = "MEDASSIST_CONFIG_PATH";
    private const string _sharedFileName = "appsettings.shared.json";

    /// <summary>
    /// Inserts the shared configuration file at the lowest priority position so that
    /// per-project appsettings and environment variables always take precedence.
    /// </summary>
    public static IConfigurationBuilder AddSharedConfiguration(this IConfigurationBuilder builder)
    {
        var path = ResolveSharedConfigPath();
        if (path is null)
        {
            return builder;
        }

        builder.Sources.Insert(0, new JsonConfigurationSource
        {
            Path = path,
            Optional = true,
            ReloadOnChange = false
        });

        return builder;
    }

    private static string? ResolveSharedConfigPath()
    {
        // 1. Explicit override — used by Docker and CI
        var explicitDir = Environment.GetEnvironmentVariable(_configPathEnvVar);
        if (explicitDir is not null)
        {
            return Path.Combine(explicitDir, _sharedFileName);
        }

        // 2. Walk up from the app's base directory to find the solution root
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateFiles("*.sln").Any())
            {
                return Path.Combine(dir.FullName, "config", _sharedFileName);
            }

            dir = dir.Parent;
        }

        return null;
    }
}
