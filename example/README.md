# Backpack example stack

`docker-compose.yml` runs a small but complete Backpack on one machine: the infrastructure
it needs and enough Backpack services to mirror npm packages into S3 storage.

> [!WARNING]
> This is for evaluation. The passwords are well known, the services run with defaults,
> and nothing is backed up. See the [Deployment Guide](../docs/DeploymentGuide.md) for
> real deployments.

## What it runs

| Service | Image | Address on the host |
|---------|-------|---------------------|
| `gui` | `ghcr.io/linus-berg/backpack/gui` | <http://localhost:3000> |
| `api` | `ghcr.io/linus-berg/backpack/integration/api` | <http://localhost:8004> |
| `gateway`, `scheduler`, `collector-router` | `ghcr.io/linus-berg/backpack/...` | — |
| `processor-npm`, `collector-http` | `ghcr.io/linus-berg/backpack/...` | — |
| `keycloak` (OIDC) | Keycloak 24 | <http://keycloak:8090> (needs the hosts entry below) |
| `rabbitmq` | `masstransit/rabbitmq` | management UI <http://localhost:15672> |
| `s3` | SeaweedFS | S3 API `localhost:9000`, admin UI <http://localhost:9001> |
| `mongo`, `mongo-express` | MongoDB 7 | Mongo Express <http://localhost:8081> |
| `redis` | Redis 7 | — |

Every user name and password defaults to `backpack` / `backpack_pass` (Keycloak's admin
console: `admin` / `backpack_pass`). To change them, set `MONGO_PASS`, `RABBIT_PASS`,
`REDIS_PASS`, `S3_ACCESS_KEY`, `S3_SECRET_KEY` or `KC_PASS` when you start Compose, and
change the same values in `vars.env`, which configures the Backpack services.

## Start it

1. Make `keycloak` resolve to your machine:

   ```text
   127.0.0.1 keycloak
   ```

   Add that line to `/etc/hosts` (Windows: `C:\Windows\System32\drivers\etc\hosts`). The
   browser and the API both have to reach Keycloak under the URL in `BP_OIDC_AUTHORITY`,
   because the issuer in the tokens has to match it. Inside Compose that name is
   `keycloak`, so your browser needs it too.

2. Start the stack:

   ```bash
   docker compose up -d
   ```

   The API, gateway and scheduler exit if Redis isn't up yet; Compose restarts them until
   it is.

3. Open <http://localhost:3000> and sign in as `admin`. The imported realm has that user,
   with the `Administrator` role, but not with a known password: set one in the Keycloak
   admin console (<http://keycloak:8090/admin>, realm *backpack* → Users → admin →
   Credentials).

4. Add an npm package, e.g. `left-pad`. Its tarballs appear in the `backpack` bucket under
   `npm/registry.npmjs.org/...`.

## Add more ecosystems

For each ecosystem:

1. Add its processor service, e.g. `ghcr.io/linus-berg/backpack/processor/pypi`, with
   `env_file: vars.env`.
2. Add the collectors its files need. Collectors are chosen by URL scheme: `http`/`https`
   (`collector/http`), `git` (`collector/git`), `docker`/`oci` (`collector/container`,
   which also needs a registry in `BP_COLLECTOR_CONTAINER_REGISTRY`), and so on. See the
   [Service Inventory](../docs/ServiceInventory.md).
3. Add a processor definition whose `_id` matches the processor's ID, on the GUI's
   Processors page or in `mongo-init.js` before the first start.

## Files

- `docker-compose.yml`: the stack.
- `vars.env`: environment for every Backpack service ([reference](../docs/Configuration.md)).
- `mongo-init.js`: seeds the processor definitions on first start.
- `backpack-realm.json`: Keycloak realm `backpack` with the public client `backpack`, the
  client role `Administrator` and the user `admin`.
