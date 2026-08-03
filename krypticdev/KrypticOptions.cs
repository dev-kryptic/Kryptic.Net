namespace KrypticDev;

/// <summary>
/// Options for <c>AddKryptic()</c>. Environment variables always take precedence:
/// KRYPTIC_PROJECT_ID, KRYPTIC_ENV, KRYPTIC_SOCKET_PATH, KRYPTIC_TIMEOUT_MS,
/// KRYPTIC_DISABLED, KRYPTIC_SILENT (see the environment variable reference at docs.kryptic.dev).
/// </summary>
public class KrypticOptions
{
    /// <summary>Override the environment (default: kryptic.json defaultEnvironment, then "development").</summary>
    public string? Environment { get; set; }

    /// <summary>Override the project id from kryptic.json.</summary>
    public string? ProjectId { get; set; }

    /// <summary>Daemon connection timeout in milliseconds.</summary>
    public int SocketTimeoutMs { get; set; } = 2000;

    /// <summary>Suppress the warning when the daemon is absent.</summary>
    public bool FallbackSilently { get; set; }
}
