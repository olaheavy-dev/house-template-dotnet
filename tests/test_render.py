"""The rendered layer: files, answers, what it adds to the base Makefile, and its build rules."""

from __future__ import annotations

import fnmatch
import json
import re
import shutil
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


def test_logs_are_json_lines_outside_development(render):
    # Production log collectors parse one JSON object per line; developers read the simple format.
    host = render() / "backend/src/Api/Api"
    console = json.loads((host / "appsettings.json").read_text())["Logging"]["Console"]
    assert console["FormatterName"] == "json"
    assert console["FormatterOptions"]["IncludeScopes"] is True
    development = json.loads((host / "appsettings.Development.json").read_text())["Logging"]["Console"]
    assert development["FormatterName"] == "simple"


def test_telemetry_names_the_service_after_the_project(render):
    # Codex review on PR 7: without a name, every app exports as unknown_service:dotnet.
    settings = json.loads((render() / "backend/src/Api/Api/appsettings.json").read_text())
    assert settings["OpenTelemetry"]["ServiceName"] == "demo-app-api"


def test_the_access_log_is_on_in_the_settings(render):
    levels = json.loads((render() / "backend/src/Api/Api/appsettings.json").read_text())["Logging"]["LogLevel"]
    assert levels["Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware"] == "Information"


def test_the_env_example_names_only_settings_the_api_has(render):
    # A documented setting that no longer exists would silently do nothing.
    example = (render() / "backend/.env.example").read_text()
    options = (BACKEND / "src/Api/Api/ApiOptions.cs").read_text()
    names = set(re.findall(r"Api__(\w+?)(?:__\d+)?=", example))
    assert names == {"CorsOrigins", "MaxRequestBodyBytes", "RequestTimeoutSeconds", "RateLimitPerMinute"}
    for name in names:
        assert re.search(rf"public \S+ {name} {{ get; init; }}", options), name
    auth = (BACKEND / "src/Api/Api/AuthOptions.cs").read_text()
    auth_names = set(re.findall(r"Auth__(\w+)=", example))
    assert auth_names == {"Issuer", "RequireHttpsMetadata", "MetadataAddress", "Audience"}
    for name in auth_names:
        assert re.search(rf"public \S+ {name} {{ get; init; }}", auth), name


def test_the_local_realm_gives_every_user_the_permissions_the_api_declares():
    # A permission an endpoint requires but no Keycloak role grants would lock everyone out.
    realm = json.loads((BACKEND / "keycloak/realm.json").read_text())
    assert realm["realm"] == "app"
    roles = {role["name"] for role in realm["roles"]["client"]["api"]}
    declared = set(re.findall(r'public const string \w+ = "([\w.]+)";',
                              (BACKEND / "src/Modules/Platform/Modules.Platform.Presentation/PlatformPermissions.cs").read_text()))
    assert declared and declared <= roles
    default = next(role for role in realm["roles"]["realm"] if role["name"] == realm["defaultRole"]["name"])
    assert declared <= set(default["composites"]["client"]["api"])
    assert "*" not in roles


def test_make_dev_backend_runs_in_development_with_the_env_file(render):
    dev = make_dry_run(render(), "dev-backend")
    assert "export ASPNETCORE_ENVIRONMENT=Development" in dev
    assert ". ./backend/.env" in dev


def test_the_api_title_reaches_the_hosts_settings(render):
    settings = json.loads((render(api_title="Course Lens API") / "backend/src/Api/Api/appsettings.json").read_text())
    assert settings["Api"]["Title"] == "Course Lens API"


TITLES = ['Acme "Public" API', "Back\\slash API", "Bob's <API> & café"]


@pytest.mark.parametrize("title", TITLES)
def test_any_title_stays_valid_json_in_the_settings(render, title):
    # Codex review on PR 2: a quote or backslash in the title broke appsettings.json, so the API
    # couldn't start.
    settings = json.loads((render(api_title=title) / "backend/src/Api/Api/appsettings.json").read_text())
    assert settings["Api"]["Title"] == title


@pytest.mark.parametrize("title", TITLES)
def test_the_openapi_document_holds_the_title_as_the_build_writes_it(render, title):
    # make check fails when the committed document differs from the build's by a single byte, and
    # the generator escapes only a quote and a backslash: Jinja's tojson would also escape ', <, >,
    # & and every non-ASCII character, so a fresh project would fail its first check.
    text = (render(api_title=title) / "backend/openapi/v1.json").read_text()
    written = title.replace("\\", "\\\\").replace('"', '\\"')
    assert f'    "title": "{written}",\n' in text
    assert json.loads(text)["info"]["title"] == title
    assert text.endswith("}\n")


@pytest.mark.parametrize("title", ["Tab\tAPI", "Two\nlines"])
def test_layer_refuses_a_title_with_control_characters(render, title):
    with pytest.raises(REFUSED):
        render(api_title=title)


def test_the_template_document_is_the_in_place_build_with_the_placeholder_title():
    # CI builds the template in place with Api__Title set to this placeholder and diffs the result
    # against the committed template, so the template's document can't drift from the code.
    text = (BACKEND / "openapi/v1.json.jinja").read_text()
    assert '    "title": "{{ api_title_in_json }}",\n' in text
    assert "{{" not in text.replace("{{ api_title_in_json }}", "")
    workflow = (ROOT / ".github/workflows/test.yml").read_text()
    assert 'Api__Title: "{{ api_title_in_json }}"' in workflow
    assert "diff -u openapi/v1.json.jinja openapi/v1.json" in workflow


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


def make_dry_run(project, target, *variables):
    makefile = "include mk/dotnet.mk\n"
    (project / "Probe.mk").write_text(makefile)
    result = subprocess.run(["make", "-n", "-f", "Probe.mk", target, *variables], cwd=project,
                            capture_output=True, text=True, check=True)
    return result.stdout


def test_make_migrate_applies_each_modules_migrations_to_the_configured_database(render):
    migrate = make_dry_run(render(), "migrate")
    assert "ConnectionStrings__Default" in migrate
    assert "dotnet ef database update --project $project --connection" in migrate
    assert "*DbContextFactory.cs" in migrate


def test_make_migrate_bundle_builds_one_executable_per_module_and_puts_the_lock_files_back(render):
    bundle = make_dry_run(render(), "migrate-bundle")
    assert "dotnet ef migrations bundle --project $project" in bundle
    # A name with a hyphen breaks the generated bundle project's namespace.
    assert "migrate_$module" in bundle
    assert "dotnet restore backend/Backend.slnx --force-evaluate" in bundle


def test_make_migrate_bundle_is_self_contained_for_the_runtime_asked_for(render):
    project = render()
    assert "--self-contained --target-runtime linux-x64" in make_dry_run(project, "migrate-bundle", "RUNTIME=linux-x64")
    # Without a runtime the bundle needs .NET on the machine that runs it.
    assert "--self-contained" not in make_dry_run(project, "migrate-bundle")


def test_the_database_setting_in_env_example_survives_being_sourced_by_the_shell(render):
    # make migrate, seed and dev-backend source backend/.env; an unquoted semicolon would split the value.
    example = (render() / "backend/.env.example").read_text()
    line = next(line for line in example.splitlines() if line.startswith("# ConnectionStrings__Default="))
    result = subprocess.run(["bash", "-c", f'{line[2:]}\nprintf %s "$ConnectionStrings__Default"'],
                            capture_output=True, text=True, check=True)
    assert result.stdout == "Host=localhost;Port=5432;Database=app;Username=app;Password=app"


def test_a_new_module_gets_a_schema_a_context_and_a_generated_first_migration(render):
    project = render()
    module = project / "backend/templates/module/src/Modules/Sample"
    assert 'Schema = "sample-route"' in (module / "Modules.Sample.Infrastructure/Persistence/SampleDbContext.cs").read_text()
    assert (module / "Modules.Sample.Infrastructure/Persistence/SampleDbContextFactory.cs").is_file()
    assert "AddModuleDbContext<SampleDbContext>" in (module / "Modules.Sample.Infrastructure/SampleModule.cs").read_text()
    create = make_dry_run(project, "module", "NAME=Billing")
    assert "dotnet ef migrations add Initial --project backend/src/Modules/Billing/Modules.Billing.Infrastructure" in create


def test_the_host_ships_wolverines_generated_glue_code_and_make_checks_it_is_current(render):
    project = render()
    generated = list((project / "backend/src/Api/Api/Internal/Generated").rglob("*.cs"))
    assert any(path.name == "GeneratedHandlerRegistry.cs" for path in generated)
    assert "generated_code = true" in (project / "backend/.editorconfig").read_text().split("Internal/Generated")[1]
    codegen = make_dry_run(project, "codegen")
    assert "codegen write" in codegen and "Messaging__GeneratedCodePath" in codegen
    check = make_dry_run(project, "check-dotnet")
    assert "diff -r backend/src/Api/Api/Internal/Generated" in check
    # A new module's subscribers change the host's glue code.
    assert "codegen write" in make_dry_run(project, "module", "NAME=Billing")


def test_the_list_endpoint_example_documents_cursor_paging(render):
    document = json.loads((render() / "backend/openapi/v1.json").read_text())
    parameters = {p["name"] for p in document["paths"]["/v1/platform/notes"]["get"]["parameters"]}
    assert {"limit", "cursor"} <= parameters
    page = document["components"]["schemas"]["PageOfNoteResponse"]
    assert set(page["required"]) == {"items", "nextCursor"}


def test_the_idempotency_key_is_documented_on_the_endpoint_that_takes_it_and_its_table_is_migrated(render):
    project = render()
    document = json.loads((project / "backend/openapi/v1.json").read_text())
    post = document["paths"]["/v1/platform/notes"]["post"]
    header = next(p for p in post["parameters"] if p["name"] == "Idempotency-Key")
    assert header["in"] == "header" and header.get("required", False) is False
    assert (project / "backend/src/Common/Common.Infrastructure/Idempotency/CommonDbContextFactory.cs").is_file()
    migrate = make_dry_run(project, "migrate")
    assert "find backend/src -name '*DbContextFactory.cs'" in migrate


def test_the_cache_is_optional_shared_and_documented(render):
    project = render()
    example = (project / "backend/.env.example").read_text()
    assert "# ConnectionStrings__Cache=" in example
    rules = (project / ".claude/rules/dotnet.md").read_text()
    assert "HybridCache" in rules and "ConnectionStrings:Cache" in rules
def test_feature_flags_ship_with_the_example_flag_the_public_list_and_the_permission(render):
    project = render()
    settings = json.loads((project / "backend/src/Api/Api/appsettings.json").read_text())
    flags = {flag["id"]: flag for flag in settings["feature_management"]["feature_flags"]}
    assert flags["notes-list"]["enabled"] is True
    assert settings["Flags"]["Public"] == ["notes-list"]
    document = json.loads((project / "backend/openapi/v1.json").read_text())
    assert "get" in document["paths"]["/v1/flags"]
    realm = json.loads((project / "backend/keycloak/realm.json").read_text())
    assert "flags.read" in {role["name"] for role in realm["roles"]["client"]["api"]}
    assert "feature_management__feature_flags__0__enabled" in (project / "backend/.env.example").read_text()
def test_the_compiler_reference_does_not_make_the_lock_file_depend_on_the_configuration(render):
    project = render()
    csproj = (project / "backend/src/Api/Api/Api.csproj").read_text()
    # A reference only some configurations have gives them different lock files, and a Release build rewrites the committed one.
    reference = next(line for line in csproj.splitlines() if "WolverineFx.RuntimeCompilation" in line)
    assert "Condition" not in reference and "IncludeAssets" in reference
    lock = json.loads((project / "backend/src/Api/Api/packages.lock.json").read_text())
    assert "WolverineFx.RuntimeCompilation" in lock["dependencies"]["net10.0"]
    check = make_dry_run(project, "check-dotnet")
    assert "--configuration Release -p:RestoreLockedMode=true" in check


def test_every_skills_frontmatter_is_valid_yaml_with_a_name_and_description(render):
    skills = sorted((render() / ".claude/skills").glob("*/SKILL.md"))
    assert {skill.parent.name for skill in skills} >= {"add-module", "add-endpoint"}
    for skill in skills:
        _, frontmatter, _ = skill.read_text().split("---\n", 2)
        meta = yaml.safe_load(frontmatter)
        assert meta["name"] == skill.parent.name, skill
        assert isinstance(meta["description"], str) and meta["description"], skill


def test_the_add_endpoint_skill_names_files_and_commands_that_exist(render):
    project = render()
    skill = (project / ".claude/skills/add-endpoint/SKILL.md").read_text()
    assert skill.startswith("---\nname: add-endpoint\n")
    targets = {line.split(":")[0] for mk in (project / "mk").glob("*.mk") for line in mk.read_text().splitlines() if line and not line.startswith(("\t", "#", " "))}
    # `make check` is base's umbrella; this layer's part of it is check-dotnet.
    for target, defined in (("openapi-backend", "openapi-backend"), ("codegen", "codegen"), ("migrate", "migrate"), ("check", "check-dotnet")):
        assert f"`make {target}`" in skill and defined in targets, target
    platform = project / "backend/src/Modules/Platform"
    for example in (
        "Modules.Platform.Presentation/NotesEndpoint.cs",
        "Modules.Platform.Presentation/PlatformEndpoints.cs",
        "Modules.Platform.Presentation/PlatformPermissions.cs",
        "Modules.Platform.Presentation/PlatformFlags.cs",
        "Modules.Platform.Application/Notes/INoteStore.cs",
        "Modules.Platform.Application/Notes/INoteReader.cs",
        "Modules.Platform.Application/Notes/NoteCache.cs",
        "Modules.Platform.Application/Notes/GetNote.cs",
    ):
        assert (platform / example).is_file(), example
    for name in ("PageRequestValidator", "WithIdempotencyKey", "WithFeatureGate", "RequirePermission", "HybridCache", "NoteCache.KeyFor"):
        assert name in skill, name
def test_the_image_is_chiseled_non_root_pinned_and_reports_its_commit(render):
    project = render()
    dockerfile = (project / "backend/Dockerfile").read_text()
    froms = [line for line in dockerfile.splitlines() if line.startswith("FROM ")]
    assert len(froms) == 2 and all("@sha256:" in line for line in froms)
    assert "dotnet/aspnet:10.0-noble-chiseled@sha256:" in froms[1]
    assert "ARG GIT_COMMIT" in dockerfile and "ENV GIT_COMMIT=" in dockerfile
    assert "USER $APP_UID" in dockerfile
    assert "dotnet restore backend/src/Api/Api/Api.csproj --locked-mode" in dockerfile
    assert 'HEALTHCHECK' in dockerfile and '"healthcheck"' in dockerfile
    ignore = (project / ".dockerignore").read_text()
    assert ignore.startswith("# Owned by house-template-dotnet") and "*\n" in ignore and "!backend/src/**" in ignore
    assert "tests" not in [line.lstrip("!") for line in ignore.splitlines() if line.startswith("!")]


def test_compose_runs_the_backend_as_production_would_on_loopback_with_everything_pinned(render):
    project = render()
    compose = yaml.safe_load((project / "backend/compose.yaml").read_text())
    services = compose["services"]
    assert set(services) == {"db", "keycloak", "dashboard", "valkey", "migrate", "api"}
    for name in ("db", "keycloak", "dashboard", "valkey"):
        assert "@sha256:" in services[name]["image"], name
    for name, service in services.items():
        for port in service.get("ports", []):
            assert str(port).startswith("127.0.0.1:"), (name, port)
    api = services["api"]
    # The host gives requests in flight 30 seconds after SIGTERM; compose must wait at least that long.
    assert int(api["stop_grace_period"].rstrip("s")) >= 30
    assert api["depends_on"]["migrate"]["condition"] == "service_completed_successfully"
    assert services["migrate"]["command"] == ["migrate"]
    assert services["migrate"]["restart"] == "no"
    assert services["valkey"]["profiles"] == ["cache"]
    build = services["api"]["build"]
    assert build["dockerfile"] == "backend/Dockerfile" and build["context"] == ".."
    environment = services["api"]["environment"]
    # An env_file overrides the image's own setting, so Production is set here, where it wins over backend/.env.
    assert environment["ASPNETCORE_ENVIRONMENT"] == "Production"
    assert api["depends_on"]["dashboard"]["condition"] == "service_started", "make up starts only the api and what it depends on"
    assert environment["Auth__MetadataAddress"].startswith("http://keycloak:8080/")
    assert environment["Auth__Issuer"].startswith("http://localhost:")
    keycloak = services["keycloak"]
    assert "--import-realm" in keycloak["command"]
    assert any(volume.startswith("./keycloak/realm.json:/opt/keycloak/data/import/") for volume in keycloak["volumes"])


def test_a_copy_of_env_example_cannot_turn_the_compose_api_into_a_development_server(render):
    project = render()
    if not shutil.which("docker"):
        pytest.skip("needs docker compose to resolve the file")
    # backend/.env.example says Development (for make dev-backend); copied to backend/.env it is loaded into the container.
    (project / "backend/.env").write_text("ASPNETCORE_ENVIRONMENT=Development\nConnectionStrings__Default=Host=elsewhere\n")
    result = subprocess.run(["docker", "compose", "-f", "backend/compose.yaml", "config", "--format", "json"],
                            cwd=project, capture_output=True, text=True, check=True)
    api = json.loads(result.stdout)["services"]["api"]["environment"]
    assert api["ASPNETCORE_ENVIRONMENT"] == "Production"
    # The settings the compose file sets itself win over backend/.env.
    assert api["ConnectionStrings__Default"].startswith("Host=db;")


def test_the_smoke_test_has_its_own_compose_project_and_ports(render):
    script = (render() / "tools/smoke-compose.sh").read_text()
    # It deletes volumes when it ends: they must be its own, never those of a stack in use.
    assert 'project="$(basename "$PWD"' in script and '-smoke"' in script
    assert 'compose() { docker compose -p "$project" "$@"; }' in script
    assert [line for line in script.splitlines() if "docker compose" in line and not line.startswith("compose()")] == []
    for port in ("API_PORT", "KEYCLOAK_PORT", "DB_PORT", "DASHBOARD_PORT"):
        assert f"{port}=1" in script, port


def test_compose_is_valid_and_make_has_targets_to_run_it(render):
    project = render()
    if shutil.which("docker"):
        result = subprocess.run(["docker", "compose", "-f", "backend/compose.yaml", "config", "--quiet"], cwd=project, capture_output=True, text=True)
        assert result.returncode == 0, result.stderr
    assert "docker compose up -d --build --wait api" in make_dry_run(project, "up")
    # GIT_COMMIT goes to Compose only: exported by make, it would reach the tests and change what they see.
    assert "export GIT_COMMIT" not in (project / "mk/dotnet.mk").read_text()
    assert "docker compose up -d --wait db keycloak dashboard" in make_dry_run(project, "db-up")
    assert "docker compose build api" in make_dry_run(project, "image")
    assert "docker compose down" in make_dry_run(project, "down")


def test_a_workflow_runs_the_smoke_test_and_the_image_files_are_template_owned(render):
    project = render()
    workflow = yaml.safe_load((project / ".github/workflows/image.yml").read_text())
    steps = workflow["jobs"]["smoke"]["steps"]
    assert any(step.get("run") == "make smoke" for step in steps)
    assert all("@" in step["uses"] and len(step["uses"].split("@")[1].split(" ")[0]) == 40 for step in steps if "uses" in step)
    assert workflow["permissions"] == {} and workflow["jobs"]["smoke"]["permissions"] == {"contents": "read"}
    assert "bash tools/smoke-compose.sh" in make_dry_run(project, "smoke")
    script = (project / "tools/smoke-compose.sh").read_text()
    assert script.startswith("#!/usr/bin/env bash") and "set -euo pipefail" in script
    owned = (project / ".github/template-owned/dotnet.txt").read_text().splitlines()
    for path in ("backend/Dockerfile", "backend/compose.yaml", ".dockerignore", "tools/smoke-compose.sh",
                 ".github/workflows/image.yml", ".claude/skills/add-endpoint/SKILL.md"):
        assert path in owned, path


def test_make_seed_runs_the_seeders_in_development(render):
    seed = make_dry_run(render(), "seed")
    assert "ASPNETCORE_ENVIRONMENT=Development" in seed
    assert "dotnet run --project backend/src/Api/Api -- seed" in seed


def test_the_platform_module_ships_its_first_migration_and_the_ef_tool(render):
    project = render()
    migrations = list((project / "backend/src/Modules/Platform/Modules.Platform.Infrastructure/Persistence/Migrations").glob("*_Initial.cs"))
    assert len(migrations) == 1
    assert "dotnet-ef" in json.loads((project / ".config/dotnet-tools.json").read_text())["tools"]


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


def test_the_openapi_document_has_one_stack():
    # Microsoft.AspNetCore.OpenApi writes the document; a second generator would describe the API
    # differently from the file the Next.js layer reads.
    packages = (BACKEND / "Directory.Packages.props").read_text()
    assert "Microsoft.AspNetCore.OpenApi" in packages
    for name in ("Swashbuckle", "NSwag"):
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
    # Its example endpoint changes the OpenAPI document, which make check would then find stale.
    assert "dotnet build backend/src/Api/Api" in module


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


def test_make_check_fails_on_a_stale_openapi_document_without_rewriting_it(render):
    check = make_dry_run(render(), "check-dotnet")
    assert "-p:OpenApiDocumentsDirectory=" in check and ".artifacts/openapi" in check
    assert "cmp -s backend/openapi/v1.json .artifacts/openapi/v1.json" in check


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
    assert set(tools) == {"jetbrains.resharper.globaltools", "nuget-license", "dotnet-ef"}
    assert all(tool["rollForward"] is False and re.fullmatch(r"[\d.]+", tool["version"]) for tool in tools.values())


def test_every_accepted_stale_package_has_a_reason_and_an_end_date():
    from datetime import date
    for entry in json.loads((BACKEND / "package-health.json").read_text()):
        assert entry["Reason"].strip(), entry["Id"]
        assert date.fromisoformat(entry["AcceptedUntil"]) > date(2026, 10, 27), entry["Id"]
