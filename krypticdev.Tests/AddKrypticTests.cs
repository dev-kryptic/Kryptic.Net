using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KrypticDev;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KrypticDev.Tests;

/// <summary>
/// Tests run against a mock daemon: a unix-socket listener speaking PROTOCOL.md v1.
/// Sequential (xunit collection) because they mutate process environment variables.
/// </summary>
[Collection("environment")]
public class AddKrypticTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalCwd;
    private Socket? _server;

    public AddKrypticTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("kryptic-sdk-").FullName;
        File.WriteAllText(Path.Combine(_tempDir, "kryptic.json"), """{ "projectId": "proj_test123456" }""");
        _originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_tempDir);

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("KRYPTIC_SILENT", "true");
        foreach (var name in new List<string> { "KRYPTIC_DISABLED", "KRYPTIC_PROJECT_ID", "KRYPTIC_ENV", "INJECTED_KEY", "EXISTING_KEY" })
            Environment.SetEnvironmentVariable(name, null);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        _server?.Dispose();
        Environment.SetEnvironmentVariable("KRYPTIC_SOCKET_PATH", null);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("INJECTED_KEY", null);
        Environment.SetEnvironmentVariable("EXISTING_KEY", null);
    }

    private void StartMockDaemon(Func<JsonElement, object> handler)
    {
        var socketPath = Path.Combine(_tempDir, "daemon.sock");
        _server = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _server.Bind(new UnixDomainSocketEndPoint(socketPath));
        _server.Listen(1);
        Environment.SetEnvironmentVariable("KRYPTIC_SOCKET_PATH", socketPath);

        _ = Task.Run(async () =>
        {
            while (true)
            {
                Socket connection;
                try { connection = await _server.AcceptAsync(); }
                catch (Exception) { return; }

                using (connection)
                {
                    var buffer = new byte[8192];
                    var received = new StringBuilder();
                    while (!received.ToString().Contains('\n'))
                    {
                        var read = await connection.ReceiveAsync(buffer);
                        if (read == 0) break;
                        received.Append(Encoding.UTF8.GetString(buffer, 0, read));
                    }

                    var request = JsonDocument.Parse(received.ToString().Split('\n')[0]).RootElement;
                    var response = JsonSerializer.Serialize(handler(request), new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    await connection.SendAsync(Encoding.UTF8.GetBytes(response + "\n"));
                }
            }
        });
    }

    private static IConfiguration BuildWithKryptic()
    {
        return new ConfigurationBuilder().AddKryptic().Build();
    }

    [Fact]
    public void InjectsSecretsIntoConfigurationAndEnvironment()
    {
        StartMockDaemon(request =>
        {
            Assert.Equal("secrets", request.GetProperty("type").GetString());
            Assert.Equal("proj_test123456", request.GetProperty("projectId").GetString());
            Assert.Equal("development", request.GetProperty("environment").GetString());
            return new { v = 1, ok = true, secrets = new List<object> { new { key = "INJECTED_KEY", value = "from-daemon" } } };
        });

        var configuration = BuildWithKryptic();

        Assert.Equal("from-daemon", configuration["INJECTED_KEY"]);
        Assert.Equal("from-daemon", Environment.GetEnvironmentVariable("INJECTED_KEY"));
    }

    [Fact]
    public void NeverOverwritesExistingEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("EXISTING_KEY", "real-env-wins");
        StartMockDaemon(_ => new { v = 1, ok = true, secrets = new List<object> { new { key = "EXISTING_KEY", value = "x" } } });

        var configuration = BuildWithKryptic();

        // IConfiguration exposes the daemon value, but the process env is untouched.
        Assert.Equal("x", configuration["EXISTING_KEY"]);
        Assert.Equal("real-env-wins", Environment.GetEnvironmentVariable("EXISTING_KEY"));
    }

    [Fact]
    public void IsANoOpWhenDaemonIsMissing()
    {
        Environment.SetEnvironmentVariable("KRYPTIC_SOCKET_PATH", Path.Combine(_tempDir, "missing.sock"));

        var configuration = BuildWithKryptic();

        Assert.Null(configuration["ANYTHING"]);
    }

    [Fact]
    public void IsANoOpOutsideDevelopment()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        StartMockDaemon(_ => new { v = 1, ok = true, secrets = new List<object> { new { key = "INJECTED_KEY", value = "x" } } });

        var configuration = BuildWithKryptic();

        Assert.Null(configuration["INJECTED_KEY"]);
    }

    [Fact]
    public void IsANoOpWhenDisabled()
    {
        Environment.SetEnvironmentVariable("KRYPTIC_DISABLED", "true");
        StartMockDaemon(_ => new { v = 1, ok = true, secrets = new List<object> { new { key = "INJECTED_KEY", value = "x" } } });

        var configuration = BuildWithKryptic();

        Assert.Null(configuration["INJECTED_KEY"]);
        Environment.SetEnvironmentVariable("KRYPTIC_DISABLED", null);
    }

    [Fact]
    public void HandlesDaemonErrorResponsesWithoutThrowing()
    {
        StartMockDaemon(_ => new { v = 1, ok = false, error = "access_denied", message = "no access" });

        var configuration = BuildWithKryptic();

        Assert.Null(configuration["INJECTED_KEY"]);
    }

    [Fact]
    public void OptionsOverrideKrypticJson()
    {
        string? seenProject = null;
        string? seenEnvironment = null;
        StartMockDaemon(request =>
        {
            seenProject = request.GetProperty("projectId").GetString();
            seenEnvironment = request.GetProperty("environment").GetString();
            return new { v = 1, ok = true, secrets = new List<object>() };
        });

        new ConfigurationBuilder()
            .AddKryptic(options =>
            {
                options.ProjectId = "proj_override0001";
                options.Environment = "staging";
            })
            .Build();

        Assert.Equal("proj_override0001", seenProject);
        Assert.Equal("staging", seenEnvironment);
    }
}
