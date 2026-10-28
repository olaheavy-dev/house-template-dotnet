# syntax=docker/dockerfile:1
# Owned by house-template-dotnet. The API image. Build it from the project root (it needs global.json):
#   docker build -f backend/Dockerfile --build-arg GIT_COMMIT=$(git rev-parse HEAD) .
# Dependencies are restored from the lock files (locked mode, as `make check` does), the API is published
# without the tests, and the result runs on the chiseled ASP.NET image: no shell, no package manager, a
# non-root user. Base images are pinned by digest; Dependabot updates them.

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
WORKDIR /src
COPY global.json ./
COPY backend/Directory.Build.props backend/Directory.Packages.props backend/nuget.config backend/.editorconfig backend/
COPY backend/src backend/src
# The OpenAPI document is written by `make openapi-backend`; an image build has no use for it.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore backend/src/Api/Api/Api.csproj --locked-mode
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish backend/src/Api/Api/Api.csproj --configuration Release --no-restore \
        -p:OpenApiGenerateDocumentsOnBuild=false -p:UseAppHost=false --output /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled@sha256:c89a6a61f494f20a5ae302990e814627a8d91b332fd4a8e7c1707c2d4da9555d
WORKDIR /app
COPY --from=build /out /app
# The commit this image was built from: /health/live reports it, so what is running can be told from what was deployed.
ARG GIT_COMMIT=
ENV GIT_COMMIT=${GIT_COMMIT} \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
# The chiseled image's own non-root user (APP_UID, 1654).
USER $APP_UID
EXPOSE 8080
# On SIGTERM the host stops taking requests and gives those in flight 30 seconds (the .NET default) to
# finish: give the container at least that long to stop (compose: stop_grace_period; Kubernetes:
# terminationGracePeriodSeconds). One process per container: scale with replicas. Migrations run apart,
# from this image (`dotnet Api.dll migrate`), once before a rollout, never in every replica at start-up.
HEALTHCHECK --interval=10s --timeout=5s --start-period=20s --start-interval=1s --retries=5 \
    CMD ["dotnet", "Api.dll", "healthcheck"]
ENTRYPOINT ["dotnet", "Api.dll"]
