#!/usr/bin/env python3
"""Resolve the NuGet version for Kryptic.Daemon.Client.

Patch (third number) auto-increments from the latest nuget.org release when
the incoming csproj version keeps the same major and minor.

If this commit already changed major or minor (1.1.0, 2.0.0, …), that version
is published as-is. The first publish (package not on nuget.org yet) also
keeps the incoming version.
"""

from __future__ import annotations

import json
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

PACKAGE_ID = "kryptic.daemon.client"
VERSIONS_URL = f"https://api.nuget.org/v3-flatcontainer/{PACKAGE_ID}/index.json"
CSPROJ = Path("Kryptic.Daemon.Client/Kryptic.Daemon.Client.csproj")
STABLE = re.compile(r"^(\d+)\.(\d+)\.(\d+)$")


def parse(version: str) -> tuple[int, int, int] | None:
    match = STABLE.match(version.strip())
    if not match:
        return None
    return int(match.group(1)), int(match.group(2)), int(match.group(3))


def read_csproj_version() -> str:
    text = CSPROJ.read_text(encoding="utf-8")
    match = re.search(r"<Version>([^<]+)</Version>", text)
    if not match:
        raise SystemExit(f"No <Version> found in {CSPROJ}")
    return match.group(1).strip()


def latest_published() -> tuple[int, int, int] | None:
    try:
        with urllib.request.urlopen(VERSIONS_URL) as response:
            payload = json.load(response)
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise
    parsed = [item for item in (parse(v) for v in payload.get("versions", [])) if item]
    return max(parsed) if parsed else None


def render(version: tuple[int, int, int]) -> str:
    return f"{version[0]}.{version[1]}.{version[2]}"


def main() -> None:
    incoming_raw = sys.argv[1] if len(sys.argv) > 1 and sys.argv[1] else read_csproj_version()
    incoming = parse(incoming_raw)
    if incoming is None:
        raise SystemExit(f"Incoming version must be major.minor.patch, got: {incoming_raw}")

    published = latest_published()
    if published is None:
        resolved = incoming
        reason = "first publish, keep incoming version"
    elif incoming[0] != published[0] or incoming[1] != published[1]:
        resolved = incoming
        reason = "major or minor changed, keep incoming version"
    else:
        resolved = (published[0], published[1], published[2] + 1)
        reason = "same major.minor, bump patch"

    print(f"incoming={render(incoming)}", file=sys.stderr)
    print(
        f"published={render(published) if published else '(none)'}",
        file=sys.stderr,
    )
    print(f"reason={reason}", file=sys.stderr)
    print(render(resolved))


if __name__ == "__main__":
    main()
