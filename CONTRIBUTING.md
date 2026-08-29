# Contributing

Thanks for taking the time to improve this Kryptic language package.

This repository is a thin daemon client. It talks to the local Kryptic daemon
over the protocol in
[Kryptic.Daemon PROTOCOL.md](https://github.com/dev-kryptic/Kryptic.Daemon/blob/main/PROTOCOL.md)
and injects development secrets into the host process. Keep it that way: no
network calls to Kryptic APIs, no extra frameworks, no custom crypto.

## What we accept

- Bug fixes in the inject / fetch path
- Test coverage for existing behaviour
- Compatibility fixes for supported runtimes
- Documentation corrections in this README

Open an issue first for larger changes (new configuration keys, protocol
changes, production-mode behaviour). Protocol changes belong in
[Kryptic.Daemon](https://github.com/dev-kryptic/Kryptic.Daemon) and must land
in every language package together.

## What we do not accept

- Features that contact the Kryptic platform directly
- Changes that throw or panic when the daemon is missing
- Overwriting environment variables or properties that are already set
- Security reports filed as public issues (email security@kryptic.dev)

## Development

```bash
dotnet test
```

## Licensing of contributions

This repository is Apache-2.0. By opening a pull request you confirm the
contribution is your own work (or you have the right to submit it) and you
license it under Apache-2.0. There is no CLA.

## Code of conduct

See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
