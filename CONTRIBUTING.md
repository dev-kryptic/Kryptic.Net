# Contributing

This repository is the .NET daemon client for Kryptic (`Kryptic.Daemon.Client`).

## What we accept

- Bug fixes and test coverage
- Documentation corrections
- Compatibility fixes

## What we do not accept

- Storing or logging secret values in plaintext
- Breaking changes to the daemon IPC protocol without a coordinated Daemon release

- Public GitHub issues for vulnerabilities (email security@kryptic.dev)

## Development

```bash
dotnet test
```

Protocol details: [Kryptic.Daemon/PROTOCOL.md](https://github.com/dev-kryptic/Kryptic.Daemon/blob/main/PROTOCOL.md).

## Releasing

Merges to `main` run tests only. Publish by pushing a `vX.Y.Z` tag (or
re-running the workflow with that tag). The tag is the version. Leave
release-worthy notes under **Unreleased**. The publish job creates the
`## X.Y.Z` section from that body if it is missing (or a one-line fallback).

## Licensing of contributions

This repository is Apache-2.0. By opening a pull request you confirm the
contribution is your own work (or you have the right to submit it) and you
license it under Apache-2.0. There is no CLA.
