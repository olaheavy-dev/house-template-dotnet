"""tools/check_package_health.py, with nuget.org faked."""

from __future__ import annotations

import importlib.util
import json
import sys
from datetime import UTC, datetime

import pytest

from conftest import TEMPLATE

spec = importlib.util.spec_from_file_location("check_package_health", TEMPLATE / "tools/check_package_health.py")
health = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = health  # dataclasses look their module up while the class is built
spec.loader.exec_module(health)

NOW = datetime(2026, 10, 27, tzinfo=UTC)


def registration(*releases: tuple[str, str]) -> dict:
    return {"items": [{"items": [{"catalogEntry": {"version": v, "published": p}} for v, p in releases]}]}


@pytest.fixture
def backend(tmp_path, monkeypatch):
    (tmp_path / "backend").mkdir()
    (tmp_path / "backend/Directory.Packages.props").write_text(
        '<Project><ItemGroup><PackageVersion Include="Fresh" Version="1.0.0" />'
        '<PackageVersion Include="Old" Version="1.0.0" /></ItemGroup></Project>')
    monkeypatch.chdir(tmp_path)
    return tmp_path


FEED = {
    "fresh": registration(("1.0.0", "2026-09-01T00:00:00+00:00"), ("2.0.0-beta", "2026-10-20T00:00:00+00:00")),
    "old": registration(("0.9.0", "2024-01-01T00:00:00+00:00"), ("1.0.0", "2025-01-01T00:00:00+00:00")),
}


def fetch(url: str) -> dict:
    return FEED[url.split("/")[-2]]


def test_a_package_with_no_release_for_a_year_fails(backend, capsys):
    assert health.main([], fetch=fetch, now=NOW) == 1
    assert "STALE: Old has had no release for 664 days" in capsys.readouterr().err


def test_prereleases_dont_count_as_releases(backend):
    assert health.latest_stable("Fresh", fetch).version == "1.0.0"


def test_an_accepted_package_passes_until_its_date(backend):
    (backend / "backend/package-health.json").write_text(
        json.dumps([{"Id": "Old", "Reason": "r", "AcceptedUntil": "2027-01-01"}]))
    assert health.main([], fetch=fetch, now=NOW) == 0


def test_an_acceptance_that_has_run_out_fails_again(backend):
    (backend / "backend/package-health.json").write_text(
        json.dumps([{"Id": "Old", "Reason": "r", "AcceptedUntil": "2026-10-01"}]))
    assert health.main([], fetch=fetch, now=NOW) == 1


def test_a_package_that_cant_be_checked_fails(backend, capsys):
    def broken(url: str) -> dict:
        raise OSError("offline")

    assert health.main([], fetch=broken, now=NOW) == 1
    assert "UNCHECKED: Fresh: offline" in capsys.readouterr().err
