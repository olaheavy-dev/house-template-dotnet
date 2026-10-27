# house-template-dotnet

The .NET layer of my Copier project templates: an ASP.NET Core modular monolith under `backend/` for a project generated from [house-template-base](https://github.com/olaheavy-dev/house-template-base), with Postgres, Keycloak, OpenAPI for the Next.js layer, architecture tests and a signed container image.

Built through reviewed pull requests; the design is in the House template plan (".NET layers").

## Develop the template

`uv run pytest` runs the repository's own tests.

`main` is protected by [`.github/rulesets/main.json`](.github/rulesets/main.json): squash merges through pull requests, with the tests, workflow lint (actionlint and zizmor), the PR title check and CodeQL required, and CodeQL and zizmor findings blocking a merge. A test checks that the required checks and the jobs the workflows run on pull requests are the same set.

## Releases

release-please turns Conventional Commit titles into a release PR and, on merge, a `vX.Y.Z` tag. Projects update from those tags.
