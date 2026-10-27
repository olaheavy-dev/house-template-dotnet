"""The rendered layer: files, answers, what it adds to the base Makefile, and its build rules."""

from __future__ import annotations

import fnmatch
import json
import re
import subprocess
import xml.etree.ElementTree as ET

import pytest
import yaml
from copier.errors import CopierAnswersInterrupt, UserMessageError

from conftest import BASE_ANSWERS, ROOT, TEMPLATE

REFUSED = (ValueError, UserMessageError, CopierAnswersInterrupt)
BACKEND = TEMPLATE / "backend"
# The module template's projects are copied by `make module`, never built in place.
PROJECTS = sorted(p.relative_to(BACKEND).as_posix() for p in BACKEND.rglob("*.csproj")
                  if not {"bin", "obj", "templates"} & set(p.parts))


def test_layer_renders_the_backend_and_its_answers(render):
    project = render()
    for path in ("global.json", ".config/dotnet-tools.json", "tools/check_package_health.py",
                 ".github/workflows/dependency-submission.yml", ".github/workflows/package-health.yml",
                 "backend/licenses/allowed.json", "backend/licenses/overrides.json", "backend/package-health.json",
                 "backend/Backend.slnx", "backend/Directory.Build.props",
                 "backend/src/Api/Api/Program.cs", "mk/dotnet.mk", ".claude/rules/dotnet.md",
                 "docs/references/dotnet.md", ".github/template-owned/dotnet.txt"):
        assert (project / path).is_file(), path
    answers = yaml.safe_load((project / ".copier-answers.dotnet.yml").read_text())
    assert answers["api_title"] == "Demo App API"


def test_the_api_title_reaches_the_hosts_settings(render):
    settings = json.loads((render(api_title="Course Lens API") / "backend/src/Api/Api/appsettings.json").read_text())
    assert settings["Api"]["Title"] == "Course Lens API"


@pytest.mark.parametrize("title", ['Acme "Public" API', "Back\\slash API", "Tab\tAPI"])
def test_any_title_stays_valid_json_in_the_settings(render, title):
    # Codex review on PR 2: a quote or backslash in the title broke appsettings.json, so the API
    # couldn't start.
    settings = json.loads((render(api_title=title) / "backend/src/Api/Api/appsettings.json").read_text())
    assert settings["Api"]["Title"] == title


def test_layer_refuses_a_project_without_base_answers(render, project):
    with pytest.raises(REFUSED):
        render(project(base=None))


def test_layer_refuses_base_answers_without_the_dotnet_stack(render, project):
    with pytest.raises(REFUSED):
        render(project(stacks=["nextjs"]))


def test_layer_refuses_an_empty_api_title(render):
    with pytest.raises(REFUSED):
        render(api_title="  ")


def test_mk_file_adds_its_targets_to_the_base_aggregates(render):
    mk = (render() / "mk/dotnet.mk").read_text()
    assert "INSTALLS += install-dotnet" in mk
    assert "CHECKS += check-dotnet" in mk
    assert "FAST_CHECKS += check-fast-dotnet" in mk


def make_dry_run(project, target):
    makefile = "include mk/dotnet.mk\n"
    (project / "Probe.mk").write_text(makefile)
    result = subprocess.run(["make", "-n", "-f", "Probe.mk", target], cwd=project,
                            capture_output=True, text=True, check=True)
    return result.stdout


def test_make_check_restores_locked_and_fails_on_format_build_or_tests(render):
    check = make_dry_run(render(), "check-dotnet")
    assert "dotnet restore backend/Backend.slnx --locked-mode" in check
    assert "dotnet format backend/Backend.slnx --verify-no-changes" in check
    assert "dotnet build backend/Backend.slnx --configuration Release" in check
    assert "dotnet test --solution backend/Backend.slnx" in check


def test_every_template_owned_entry_matches_a_file(render):
    project = render()
    files = [p.relative_to(project).as_posix() for p in project.rglob("*") if p.is_file()]
    owned = (project / ".github/template-owned/dotnet.txt").read_text().splitlines()
    for pattern in (line for line in owned if line and not line.startswith("#")):
        assert fnmatch.filter(files, pattern), f"{pattern} matches no rendered file"


def test_no_local_build_output_or_secrets_are_ever_copied(render):
    project = render()
    for path in project.rglob("*"):
        parts = set(path.relative_to(project).parts)
        assert not parts & {"bin", "obj", "TestResults", "StrykerOutput", ".artifacts"}, path
        assert not path.name.endswith(".feature.cs"), path
        assert not path.name.startswith(".env") or path.name == ".env.example", path


def test_the_solution_lists_every_project():
    # A project left out of Backend.slnx is never built or tested.
    solution = ET.parse(BACKEND / "Backend.slnx").getroot()
    listed = sorted(p.get("Path") for p in solution.iter("Project"))
    assert listed == PROJECTS


def test_every_project_has_a_committed_lock_file():
    # make check restores in locked mode, which fails without one.
    for project in PROJECTS:
        assert (BACKEND / project).parent.joinpath("packages.lock.json").is_file(), project


def test_lock_files_belong_to_the_project_after_the_first_copy():
    config = yaml.safe_load((ROOT / "copier.yml").read_text())
    assert "backend/**/packages.lock.json" in config["_skip_if_exists"]


def test_one_build_settings_file_covers_every_project():
    # A nested Directory.Build.props would let its projects drop the strict settings.
    props = [p for p in TEMPLATE.rglob("Directory.Build.props") if not {"bin", "obj"} & set(p.parts)]
    assert props == [BACKEND / "Directory.Build.props"]
    settings = (BACKEND / "Directory.Build.props").read_text()
    for setting in ("<AnalysisMode>All</AnalysisMode>", "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
                    "<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>",
                    "<NuGetAuditMode>all</NuGetAuditMode>"):
        assert setting in settings


def test_package_versions_live_only_in_directory_packages_props():
    for project in PROJECTS:
        text = (BACKEND / project).read_text()
        assert not re.search(r"<PackageReference[^>]*Version=", text), project


def test_every_package_comes_from_nuget_org_only():
    config = (BACKEND / "nuget.config").read_text()
    assert "<clear />" in config
    assert '<package pattern="*" />' in config


def test_licence_changed_packages_are_never_used():
    packages = (BACKEND / "Directory.Packages.props").read_text()
    for name in ("MediatR", "FluentAssertions", "SonarAnalyzer"):
        assert name not in packages


def test_the_sdk_pin_is_exact_and_matches_the_references():
    sdk = json.loads((TEMPLATE / "global.json").read_text())["sdk"]
    assert re.fullmatch(r"\d+\.\d+\.\d{3}", sdk["version"])
    assert sdk["allowPrerelease"] is False
    assert f"SDK {sdk['version']}" in (TEMPLATE / "docs/references/dotnet.md").read_text()


def test_rules_file_loads_only_for_backend_paths(render):
    rules = (render() / ".claude/rules/dotnet.md").read_text()
    front = yaml.safe_load(rules.split("---")[1])
    assert front["paths"] == ["backend/**", "global.json"]


def test_base_answers_fixture_matches_the_pinned_base_version():
    workflow = yaml.safe_load((ROOT / ".github/workflows/test.yml").read_text())
    assert workflow["env"]["BASE_VERSION"] == BASE_ANSWERS["_commit"]


def test_make_module_adds_a_module_from_the_template_without_installing_it(render):
    module = make_dry_run(render(), "module").replace("\\\n", " ")
    assert "dotnet new install backend/templates/module --force --debug:custom-hive" in module
    assert "dotnet new house-module --name" in module
    assert "dotnet sln backend/Backend.slnx add" in module
    assert "Api.csproj reference" in module


def test_the_module_template_holds_the_six_projects_and_a_test():
    projects = sorted(p.relative_to(BACKEND / "templates/module").as_posix()
                      for p in (BACKEND / "templates/module").rglob("*.csproj"))
    layers = ["Application", "Domain", "Infrastructure", "IntegrationEvents", "Presentation", "PublicApi"]
    assert projects == sorted([f"src/Modules/Sample/Modules.Sample.{layer}/Modules.Sample.{layer}.csproj" for layer in layers]
                              + ["tests/Modules.Sample.UnitTests/Modules.Sample.UnitTests.csproj"])
    config = json.loads((BACKEND / "templates/module/.template.config/template.json").read_text())
    assert (config["shortName"], config["sourceName"]) == ("house-module", "Sample")


def test_program_keeps_the_markers_make_module_inserts_at():
    program = (BACKEND / "src/Api/Api/Program.cs").read_text()
    assert "// make module: registrations" in program
    assert "// make module: endpoints" in program


def test_make_check_runs_the_architecture_tests_in_debug_too(render):
    assert "dotnet test --project backend/tests/ArchitectureTests --configuration Debug" in make_dry_run(render(), "check-dotnet")


def test_make_check_runs_the_licence_check_and_inspectcode(render):
    project = render()
    check = make_dry_run(project, "check-dotnet")
    assert "dotnet nuget-license" in check and "dotnet jb inspectcode" in check
    licences = make_dry_run(project, "licenses-backend")
    assert "--allowed-license-types backend/licenses/allowed.json" in licences
    assert "--include-transitive" in licences
    inspect = make_dry_run(project, "inspect-backend")
    assert "--severity=WARNING" in inspect and "ArchitectureTests.Fixtures" in inspect


def test_every_licence_override_matches_the_pinned_version():
    # An override names an exact version, so upgrading the package means looking at its licence again.
    pinned = {item.get("Include"): item.get("Version")
              for item in ET.parse(BACKEND / "Directory.Packages.props").getroot().iter("PackageVersion")}
    for override in json.loads((BACKEND / "licenses/overrides.json").read_text()):
        assert pinned.get(override["Id"]) == override["Version"], override["Id"]


def test_the_tools_are_pinned_without_roll_forward():
    tools = json.loads((TEMPLATE / ".config/dotnet-tools.json").read_text())["tools"]
    assert set(tools) == {"jetbrains.resharper.globaltools", "nuget-license"}
    assert all(tool["rollForward"] is False and re.fullmatch(r"[\d.]+", tool["version"]) for tool in tools.values())


def test_every_accepted_stale_package_has_a_reason_and_an_end_date():
    from datetime import date
    for entry in json.loads((BACKEND / "package-health.json").read_text()):
        assert entry["Reason"].strip(), entry["Id"]
        assert date.fromisoformat(entry["AcceptedUntil"]) > date(2026, 10, 27), entry["Id"]
