# krypticdev (.NET SDK)

The Kryptic .NET SDK. One line wires the whole integration:

```csharp
using KrypticDev;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddKryptic(); // that's it

var app = builder.Build();
```

When the environment is `Development` (`ASPNETCORE_ENVIRONMENT` or `DOTNET_ENVIRONMENT`),
`AddKryptic()` finds `kryptic.json`, fetches the project's secrets from the local Kryptic
daemon, and exposes them through `IConfiguration`, `IOptions<T>` and
`Environment.GetEnvironmentVariable()`. In any other environment it is a no-op, and it
never throws — no daemon just means your app starts with the configuration it already has.

```bash
dotnet add package krypticdev
```

Targets `netstandard2.0`, `net6.0` and `net8.0` — usable from .NET Framework 4.6.2+
and every .NET Core / .NET version, with the lowest workable
`Microsoft.Extensions.Configuration` dependency per target so legacy apps are never
forced to upgrade their configuration stack.

## Options

```csharp
builder.Configuration.AddKryptic(options =>
{
    options.Environment = "staging";     // override the environment
    options.SocketTimeoutMs = 2000;      // daemon connection timeout
    options.FallbackSilently = true;     // suppress the warning when the daemon is absent
    options.ProjectId = "proj_override"; // override kryptic.json
});
```

Environment variables always win: `KRYPTIC_PROJECT_ID`, `KRYPTIC_ENV`,
`KRYPTIC_SOCKET_PATH`, `KRYPTIC_TIMEOUT_MS`, `KRYPTIC_DISABLED`, `KRYPTIC_SILENT`.
Injected values never overwrite environment variables that are already set.

Protocol: see [daemon/PROTOCOL.md](https://github.com/dev-kryptic/Kryptic.Daemon/blob/main/PROTOCOL.md). License: Apache-2.0.

```bash
dotnet test
```
