using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace KrypticDev;

public class KrypticConfigurationSource(KrypticOptions options) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new KrypticConfigurationProvider(options);
    }
}

/// <summary>
/// The configuration provider behind <c>AddKryptic()</c>. In Development it fetches the
/// project's secrets from the daemon and exposes them via IConfiguration; it also sets
/// process environment variables (without overwriting existing ones) so
/// <c>Environment.GetEnvironmentVariable</c> works too. Never throws — problems degrade
/// to a single console warning (rule 1 of daemon/PROTOCOL.md).
/// </summary>
public class KrypticConfigurationProvider(KrypticOptions options) : ConfigurationProvider
{
    public override void Load()
    {
        if (!IsDevelopment()) return;
        if (Environment.GetEnvironmentVariable("KRYPTIC_DISABLED") == "true") return;

        var config = FindKrypticJson();

        var projectId = options.ProjectId
            ?? Environment.GetEnvironmentVariable("KRYPTIC_PROJECT_ID")
            ?? config?.ProjectId;
        if (string.IsNullOrEmpty(projectId))
        {
            Warn("no kryptic.json found (and no KRYPTIC_PROJECT_ID set) — nothing to inject.");
            return;
        }

        var environment = options.Environment
            ?? Environment.GetEnvironmentVariable("KRYPTIC_ENV")
            ?? config?.DefaultEnvironment
            ?? "development";

        var timeoutMs = int.TryParse(Environment.GetEnvironmentVariable("KRYPTIC_TIMEOUT_MS"), out var t)
            ? t
            : options.SocketTimeoutMs;

        DaemonClient.DaemonResponse response;
        try
        {
            // The ! is for netstandard2.0, whose IsNullOrEmpty lacks nullability flow annotations.
            response = DaemonClient.FetchSecrets(projectId!, environment, timeoutMs);
        }
        catch (Exception e)
        {
            Warn($"daemon not reachable ({e.Message}) — continuing without injected secrets.");
            return;
        }

        if (!response.Ok)
        {
            Warn($"daemon refused the request ({response.Error}): {response.Message}");
            return;
        }

        foreach (var secret in response.Secrets ?? [])
        {
            Data[secret.Key] = secret.Value;

            // Real environment always wins over injected values.
            if (Environment.GetEnvironmentVariable(secret.Key) is null)
                Environment.SetEnvironmentVariable(secret.Key, secret.Value);
        }
    }

    private static bool IsDevelopment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class KrypticJson
    {
        public string? ProjectId { get; set; }
        public string? DefaultEnvironment { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Walks up from the working directory (then the app base) looking for kryptic.json.</summary>
    private KrypticJson? FindKrypticJson()
    {
        foreach (var start in new List<string> { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "kryptic.json");
                if (File.Exists(candidate))
                {
                    try
                    {
                        return JsonSerializer.Deserialize<KrypticJson>(File.ReadAllText(candidate), JsonOptions);
                    }
                    catch (JsonException)
                    {
                        Warn($"could not parse {candidate} — ignoring it.");
                        return null;
                    }
                }
                directory = directory.Parent;
            }
        }
        return null;
    }

    private void Warn(string message)
    {
        if (options.FallbackSilently) return;
        if (Environment.GetEnvironmentVariable("KRYPTIC_SILENT") == "true") return;
        Console.Error.WriteLine($"[kryptic] {message}");
    }
}

public static class KrypticConfigurationExtensions
{
    /// <summary>
    /// The entire Kryptic integration:
    /// <c>builder.Configuration.AddKryptic();</c>
    /// </summary>
    public static IConfigurationBuilder AddKryptic(this IConfigurationBuilder builder, Action<KrypticOptions>? configure = null)
    {
        var options = new KrypticOptions();
        configure?.Invoke(options);
        return builder.Add(new KrypticConfigurationSource(options));
    }
}
