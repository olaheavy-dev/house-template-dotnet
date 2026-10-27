"""This repository's own governance: the ruleset protecting main and the release setup."""

from __future__ import annotations

import itertools
import json
import re

import yaml

from conftest import ROOT


def _job_names(job):
    """A job's check names, one per matrix combination when its name uses the matrix."""
    matrix = {k: v for k, v in job.get("strategy", {}).get("matrix", {}).items() if isinstance(v, list)}
    names = set()
    for values in itertools.product(*matrix.values()):
        name = job["name"]
        for key, value in zip(matrix, values, strict=True):
            name = name.replace(f"${{{{ matrix.{key} }}}}", str(value))
        names.add(name)
    return names


def _required_checks():
    rules = {r["type"]: r for r in json.loads((ROOT / ".github/rulesets/main.json").read_text())["rules"]}
    return {c["context"] for c in rules["required_status_checks"]["parameters"]["required_status_checks"]}


def _pull_request_checks():
    names = set()
    for path in (ROOT / ".github/workflows").glob("*.yml"):
        workflow = yaml.safe_load(path.read_text())
        triggers = workflow[True] if True in workflow else workflow["on"]  # YAML 1.1 reads `on` as True
        if "pull_request" in triggers:
            for job in workflow["jobs"].values():
                names |= _job_names(job)
    return names


def test_required_checks_are_jobs_the_workflows_run_on_pull_requests():
    required = _required_checks()
    assert required <= _pull_request_checks(), required - _pull_request_checks()


def test_every_pull_request_job_is_required():
    # A job left out of the ruleset could fail without blocking a merge.
    checks = _pull_request_checks()
    assert checks <= _required_checks(), checks - _required_checks()


def test_ruleset_gates_merges_on_code_scanning_and_squash_only():
    rules = {r["type"]: r for r in json.loads((ROOT / ".github/rulesets/main.json").read_text())["rules"]}
    assert {t["tool"] for t in rules["code_scanning"]["parameters"]["code_scanning_tools"]} == {"CodeQL", "zizmor"}
    # Zero findings: an alert of any severity, from either tool, blocks a merge.
    assert all((t["alerts_threshold"], t["security_alerts_threshold"]) == ("all", "all")
               for t in rules["code_scanning"]["parameters"]["code_scanning_tools"])
    assert rules["pull_request"]["parameters"]["allowed_merge_methods"] == ["squash"]


def test_releases_start_at_0_1_with_v_tags():
    # Releases are immutable, so the first one can't be renumbered after the fact.
    config = json.loads((ROOT / "release-please-config.json").read_text())
    assert config["include-v-in-tag"] and config["bump-minor-pre-major"]
    assert config["packages"]["."]["initial-version"] == "0.1.0"
    # release-please moves the manifest to each release, so only its shape is fixed.
    manifest = json.loads((ROOT / ".release-please-manifest.json").read_text())
    assert manifest.keys() == {"."} and re.fullmatch(r"\d+\.\d+\.\d+", manifest["."])
