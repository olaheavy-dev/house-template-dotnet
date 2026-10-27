#!/usr/bin/env python3
"""Owned by house-template-dotnet. Flags packages in backend/Directory.Packages.props whose
latest stable release on nuget.org is older than MAX_AGE_DAYS: a dependency nobody releases any
more is a risk to plan around (see the exit route in docs/references/dotnet.md).

A stale package can be accepted on purpose in backend/package-health.json, with a reason and an
AcceptedUntil date; after that date the check fails again and the decision is made afresh.

Usage: python3 tools/check_package_health.py [--max-age-days N]
Exit status 1 when any package is stale (and not accepted) or can't be checked. Standard library only.
"""

from __future__ import annotations

import argparse
import gzip
import json
import sys
import urllib.request
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path

PROPS = Path("backend/Directory.Packages.props")
ACCEPTED = Path("backend/package-health.json")
REGISTRATION = "https://api.nuget.org/v3/registration5-gz-semver2/{id}/index.json"
MAX_AGE_DAYS = 365


@dataclass(frozen=True)
class Release:
    package: str
    version: str
    published: datetime


def pinned_packages(props: Path = PROPS) -> list[str]:
    root = ET.parse(props).getroot()
    return sorted({item.attrib["Include"] for item in root.iter("PackageVersion")})


def fetch_json(url: str) -> dict:
    request = urllib.request.Request(url, headers={"Accept-Encoding": "gzip"})
    with urllib.request.urlopen(request, timeout=30) as response:
        body = response.read()
    if body[:2] == b"\x1f\x8b":
        body = gzip.decompress(body)
    return json.loads(body)


def latest_stable(package: str, fetch=fetch_json) -> Release:
    index = fetch(REGISTRATION.format(id=package.lower()))
    entries = []
    for page in index["items"]:
        items = page["items"] if "items" in page else fetch(page["@id"])["items"]
        entries += [item["catalogEntry"] for item in items]
    stable = [entry for entry in entries if "-" not in entry["version"] and entry.get("listed", True)]
    if not stable:
        raise LookupError(f"{package} has no stable release on nuget.org")
    newest = max(stable, key=lambda entry: entry["published"])
    return Release(package, newest["version"], datetime.fromisoformat(newest["published"]))


def accepted(path: Path = ACCEPTED) -> dict[str, datetime]:
    """Package id -> the date its acceptance runs out."""
    if not path.exists():
        return {}
    return {entry["Id"]: datetime.fromisoformat(entry["AcceptedUntil"]).replace(tzinfo=UTC)
            for entry in json.loads(path.read_text())}


def stale(releases: list[Release], now: datetime, max_age_days: int,
          accepted_until: dict[str, datetime] | None = None) -> list[Release]:
    accepted_until = accepted_until or {}
    return [release for release in releases
            if (now - release.published).days > max_age_days
            and not (release.package in accepted_until and now <= accepted_until[release.package])]


def main(argv: list[str] | None = None, fetch=fetch_json, now: datetime | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--max-age-days", type=int, default=MAX_AGE_DAYS)
    args = parser.parse_args(argv)
    now = now or datetime.now(UTC)
    releases, failures = [], []
    for package in pinned_packages():
        try:
            releases.append(latest_stable(package, fetch))
        except Exception as error:  # noqa: BLE001 - report every package, not just the first failure
            failures.append(f"{package}: {error}")
    exceptions = accepted()
    print(f"{'Package':47}{'Latest release':21}Published")
    for release in sorted(releases, key=lambda r: r.published):
        note = f"  (accepted until {exceptions[release.package]:%Y-%m-%d})" if release.package in exceptions else ""
        print(f"{release.package:47}{release.version:21}{release.published:%Y-%m-%d}{note}")
    old = stale(releases, now, args.max_age_days, exceptions)
    for release in old:
        print(f"STALE: {release.package} has had no release for {(now - release.published).days} days", file=sys.stderr)
    for failure in failures:
        print(f"UNCHECKED: {failure}", file=sys.stderr)
    return 1 if old or failures else 0


if __name__ == "__main__":
    sys.exit(main())
