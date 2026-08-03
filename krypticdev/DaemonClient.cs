using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace KrypticDev;

/// <summary>
/// Talks daemon/PROTOCOL.md v1: one newline-delimited JSON request, one response,
/// over a unix domain socket (macOS/Linux) or named pipe (Windows).
/// </summary>
internal static class DaemonClient
{
    private const int ProtocolVersion = 1;

    internal sealed class SecretEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    internal sealed class DaemonResponse
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public List<SecretEntry>? Secrets { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static DaemonResponse FetchSecrets(string projectId, string environment, int timeoutMs)
    {
        var request = JsonSerializer.Serialize(
            new { v = ProtocolVersion, type = "secrets", projectId, environment }, JsonOptions);

        var raw = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? RoundTripNamedPipe(request, timeoutMs)
            : RoundTripUnixSocket(request, timeoutMs);

        return JsonSerializer.Deserialize<DaemonResponse>(raw, JsonOptions)
               ?? throw new InvalidOperationException("empty response");
    }

    private static string SocketPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("KRYPTIC_SOCKET_PATH");
        if (!string.IsNullOrEmpty(overridePath)) return overridePath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            if (!string.IsNullOrEmpty(runtimeDir)) return Path.Combine(runtimeDir, "kryptic-daemon.sock");
        }

        return "/tmp/kryptic-daemon.sock";
    }

    private static string RoundTripUnixSocket(string request, int timeoutMs)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.SendTimeout = timeoutMs;
        socket.ReceiveTimeout = timeoutMs;
        socket.Connect(new UnixDomainSocketEndPoint(SocketPath()));

        socket.Send(Encoding.UTF8.GetBytes(request + "\n"));
        return ReadLine(new NetworkStream(socket, ownsSocket: false));
    }

    private static string RoundTripNamedPipe(string request, int timeoutMs)
    {
        var pipeName = Environment.GetEnvironmentVariable("KRYPTIC_SOCKET_PATH") ?? "kryptic-daemon";
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        pipe.Connect(timeoutMs);

        var payload = Encoding.UTF8.GetBytes(request + "\n");
        pipe.Write(payload, 0, payload.Length);
        pipe.Flush();
        return ReadLine(pipe);
    }

    private static string ReadLine(Stream stream)
    {
        var builder = new StringBuilder();
        var buffer = new byte[4096];

        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0) throw new IOException("connection closed");

            builder.Append(Encoding.UTF8.GetString(buffer, 0, read));
            var text = builder.ToString();
            var newline = text.IndexOf('\n');
            if (newline >= 0) return text.Substring(0, newline);
        }
    }

#if NETSTANDARD2_0
    /// <summary>
    /// sockaddr_un endpoint for runtimes whose BCL lacks UnixDomainSocketEndPoint
    /// (.NET Core 2.x, Mono). On Windows the named-pipe path is taken instead,
    /// so this never runs on .NET Framework.
    /// </summary>
    private sealed class UnixDomainSocketEndPoint(string path) : EndPoint
    {
        private readonly byte[] _encodedPath = Encoding.UTF8.GetBytes(path);

        public override AddressFamily AddressFamily => AddressFamily.Unix;

        public override SocketAddress Serialize()
        {
            // 2 bytes family prefix + path + null terminator, as in Mono's UnixEndPoint.
            var address = new SocketAddress(AddressFamily.Unix, _encodedPath.Length + 3);
            for (var i = 0; i < _encodedPath.Length; i++)
                address[2 + i] = _encodedPath[i];
            address[2 + _encodedPath.Length] = 0;
            return address;
        }
    }
#endif
}
