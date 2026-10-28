# Local sign-in (Keycloak)

`realm.json` is the realm the local Keycloak imports: realm `app`, the API's permissions as roles of
the `api` client, and two users. It is for local development and tests only: **never import it into
a shared Keycloak**. Its users, passwords and the `web` client's secret are public.

| Client | Kind | Used by |
| --- | --- | --- |
| `api` | bearer-only | The API: the tokens' audience. Its client roles are the API's permissions. |
| `web` | confidential, code flow with PKCE | The Next.js frontend, server-side, at http://localhost:3000 |
| `local-cli` | public, password grant | `curl` and the integration tests (`KeycloakTests`) |

| User | Password | Permissions |
| --- | --- | --- |
| `dev` (dev@example.com) | `dev` | Every default permission (`me.read`) |
| `visitor` (visitor@example.com) | `visitor` | None: for trying a 403 |

## Permissions

A permission is a role of the `api` client (`me.read`). The API reads them from the token's
`resource_access.api.roles` and endpoints require one with `.RequirePermission(...)`. To add one:
add the role under `roles.client.api`, give it to users (add it to `default-roles-app` when every
user should have it), and declare it beside the module's endpoints (`PlatformPermissions`).

## In Compose

`docker compose up -d --wait keycloak` (or `make db-up` / `make up`) runs this Keycloak at http://localhost:8080 with this realm imported (admin console: `admin` / `admin`). The import happens only when the realm does not exist yet, so after editing `realm.json` throw the old one away: `docker compose rm -sfv keycloak && docker compose up -d keycloak`. Tokens name `http://localhost:8080/realms/app` as their issuer; the API in Compose reads Keycloak's keys at `keycloak:8080` (`Auth__MetadataAddress`).

## A token by hand

```sh
curl -s -d grant_type=password -d client_id=local-cli -d username=dev -d password=dev \
  http://localhost:8080/realms/app/protocol/openid-connect/token | jq -r .access_token
```

Keycloak imports the realm only when it starts with an empty database; after editing `realm.json`,
remove its data volume (or change it in the admin console) to see the change.
