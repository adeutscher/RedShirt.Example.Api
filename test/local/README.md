# Local Testing Instructions

The local stack includes MariaDB (MySQL-compatible). The API expects tables that are
maintained by the separate [RedShirt.Example.Schema](https://github.com/adeutscher/RedShirt.Example.Schema) project.
Apply that schema against the local database before starting the API.

## Prerequisites

Set `LOCAL_SQL_PASSWORD` in your environment (for example in `~/.bashrc`). Docker
Compose uses it as the MariaDB root password, and the Schema project's
`local-update.sh` uses the same value to connect as `root`.

```bash
export LOCAL_SQL_PASSWORD="ExamplePassword@123"
```

If you added it to `~/.bashrc`, reload:

```bash
. ~/.bashrc
```

## Steps

1. Bring up `ministack`, `redis`, `mariadb`, `wiremock-foo`, `wiremock-bar`, and `keycloak` containers:

    ```bash
    docker compose up -d ministack redis mariadb wiremock-foo wiremock-bar keycloak
    ```

    See **Foo WireMock stubs**, **Bar WireMock stubs**, and **Authentication and authorization (Keycloak)** below.
    Admin UIs:
    http://localhost:9100/__admin/ (Foo),
    http://localhost:9101/__admin/ (Bar),
    http://localhost:9080/ (Keycloak; admin/`admin`).

2. Run `make-local-aws-resources.sh` to create ministack resources (DynamoDB table and
   SSM parameters, including `/mysql/connection-string`, `/foo/api-key`,
   `/bar/oauth/client-id`, and `/bar/oauth/client-secret`):

    ```bash
    ./make-local-aws-resources.sh
    ```

3. Apply the MySQL/MariaDB schema using
   [RedShirt.Example.Schema](https://github.com/adeutscher/RedShirt.Example.Schema).

    Clone the repo (if you do not already have it), then from that checkout run
    `local-update.sh`. That script targets `127.0.0.1` / database `example` as
    `root`, using the same `LOCAL_SQL_PASSWORD` environment variable as this Compose file's MariaDB service.

    ```bash
    git clone https://github.com/adeutscher/RedShirt.Example.Schema.git
    cd RedShirt.Example.Schema
    ./local-update.sh
    ```

    Re-run `./local-update.sh` whenever new schema scripts are added to an applied project. The DbUp library journals applied scripts so only pending updates run.

    If you are running this project specifically in its capacity as an example template and not as an applied project, then you will need to delete the `data/mariadb-data/` directory to apply schema changes. The schema example project was not made to incrementally track the history of its template form, so the example tables in support of this template are all maintained by one SQL file.

    See the Schema project's
    [README](https://github.com/adeutscher/RedShirt.Example.Schema/blob/develop/README.md)
    for environment variables, script layout, and non-local apply (`update.sh`).

4. Bring up the `api` container:

    ```bash
    docker compose up api
    ```

5. Visit the Swagger page at http://localhost:9000/swagger/

6. Obtain a bearer token (see **Authentication and authorization (Keycloak)**) and authorize Swagger or `curl`.

## Authentication and authorization (Keycloak)

Local Compose runs Keycloak on http://localhost:9080 with realm `example` imported from
`keycloak/realm-example.json`. The API container validates JWTs using:

| Variable                                   | Local default                                                                                         |
|--------------------------------------------|-------------------------------------------------------------------------------------------------------|
| `AUTHENTICATION__DISABLE_AUTHENTICATION`   | `false`                                                                                               |
| `AUTHENTICATION__AUTHORITY`                | `http://localhost:9080/realms/example` (token `iss`)                                                  |
| `AUTHENTICATION__METADATA_ADDRESS`         | `http://keycloak:8080/realms/example/.well-known/openid-configuration` (JWKS discovery inside Docker) |
| `AUTHENTICATION__AUDIENCE`                 | `example-api`                                                                                         |
| `AUTHENTICATION__REQUIRE_HTTPS_METADATA`   | `false`                                                                                               |

When authentication is enabled:

* Realm roles are mapped to permission claims (`permission` = `api:read` / `api:write`).
  Endpoints authorize on those permissions, not on role names.
* A fallback policy requires `api:write` (granted to `api-user`).
* GET endpoints marked with `[ApproveReadOnly]` require `api:read` and HTTP GET
  (granted to `api-readonly` and `api-user`).
* All GET controller actions are approved for read-only access at present.
* **Orders** also use resource-based authorization: callers without `api:write` may only
  see orders whose `CustomerId` matches the JWT `customer_id` claim. Failed checks return
  **404** (same as a missing id) so existence is not leaked. Product/Foo/Bar/ExampleItem
  stay permission-only.

Realm roles are emitted as multivalued JWT `role` claims via a client protocol mapper in
`keycloak/realm-example.json`. Locally, `api-user` is a composite role that includes
`api-readonly`; the API map also grants both permissions to `api-user` so authorization
does not depend on the IdP sending both role claims.

If you already had a Keycloak container from before roles were added, recreate it so the realm
import re-runs (`docker compose up -d --force-recreate keycloak`). Keycloak only imports a realm
when it does not already exist in that container’s data.

### Seeded clients / users / roles

| Kind                | Id / username     | Secret / password         | Notes                                              |
|---------------------|-------------------|---------------------------|----------------------------------------------------|
| Public client       | `example-api`     | _(none)_                  | Password grant for interactive testing             |
| Confidential client | `example-service` | `example-service-secret`  | Client-credentials grant; realm role `api-user`    |
| User                | `testuser`        | `testpass`                | Realm role `api-user` (full access)                |
| User                | `readonlyuser`    | `readonlypass`            | `api-readonly`; JWT `customer_id` = `11111111-1111-1111-1111-111111111111` |

| Realm role       | Permissions (API map)     | Access                                                |
|------------------|---------------------------|-------------------------------------------------------|
| `api-user`       | `api:read`, `api:write`   | All endpoints (Keycloak composite includes `api-readonly`) |
| `api-readonly`   | `api:read`                | Only GET endpoints decorated with `[ApproveReadOnly]` |

### Get a bearer token

Use the stdlib Python helper (no pip packages required):

```bash
chmod +x ./get-bearer-token.py   # once
./get-bearer-token.py            # password grant as testuser → prints access_token
./get-bearer-token.py --print-header
./get-bearer-token.py --grant client_credentials
./get-bearer-token.py --username readonlyuser --password readonlypass
# or:
./get-bearer-token.py --readonly
```

Or call Keycloak directly:

```bash
curl -s -X POST 'http://localhost:9080/realms/example/protocol/openid-connect/token' \
  -H 'Content-Type: application/x-www-form-urlencoded' \
  -d 'grant_type=password' \
  -d 'client_id=example-api' \
  -d 'username=testuser' \
  -d 'password=testpass' | jq -r .access_token
```

### Call the API

Full-access user (`api-user`):

```bash
TOKEN="$(./get-bearer-token.py)"
curl -s -H "Authorization: Bearer ${TOKEN}" 'http://localhost:9000/foo/1'
curl -s -X POST -H "Authorization: Bearer ${TOKEN}" -H 'Content-Type: application/json' \
  -d '{"name":"demo"}' 'http://localhost:9000/foo'
```

Read-only user (`api-readonly`) — GET succeeds, mutating verbs return 403:

```bash
RO_TOKEN="$(./get-bearer-token.py --readonly)"
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer ${RO_TOKEN}" 'http://localhost:9000/foo/1'
curl -s -o /dev/null -w '%{http_code}\n' -X POST -H "Authorization: Bearer ${RO_TOKEN}" \
  -H 'Content-Type: application/json' -d '{"name":"demo"}' 'http://localhost:9000/foo'
```

### Orders: customer-scoped access

`readonlyuser` has user attribute `customer_id` = `11111111-1111-1111-1111-111111111111`
(mapped into the access token). `testuser` has `api:write` and is not scoped.

Create an order as the full-access user, then fetch it as read-only:

```bash
TOKEN="$(./get-bearer-token.py)"
ORDER_JSON="$(curl -s -X POST -H "Authorization: Bearer ${TOKEN}" -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: local-order-1' \
  -d '{"customerId":"11111111-1111-1111-1111-111111111111","status":"open","totalAmount":"10.00"}' \
  'http://localhost:9000/orders')"
ORDER_ID="$(echo "${ORDER_JSON}" | jq -r .id)"

RO_TOKEN="$(./get-bearer-token.py --readonly)"
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer ${RO_TOKEN}" \
  "http://localhost:9000/orders/${ORDER_ID}"   # 200

OTHER_JSON="$(curl -s -X POST -H "Authorization: Bearer ${TOKEN}" -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: local-order-2' \
  -d '{"customerId":"22222222-2222-2222-2222-222222222222","status":"open","totalAmount":"10.00"}' \
  'http://localhost:9000/orders')"
OTHER_ID="$(echo "${OTHER_JSON}" | jq -r .id)"
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer ${RO_TOKEN}" \
  "http://localhost:9000/orders/${OTHER_ID}"   # 404
```

Search as `readonlyuser` is forced to that customer id (other `customerId` query values do not leak rows).

In Swagger UI, use **Authorize**, choose **Bearer**, and paste the access token only (no `Bearer ` prefix).

To run the API without JWT checks locally, set `AUTHENTICATION__DISABLE_AUTHENTICATION=true`. NSwag generation sets that variable in the API project’s post-build `Exec` so OpenAPI generation does not require an identity provider.

## Foo WireMock stubs

`wiremock-foo` mocks the external Foo HTTP API used by
`RedShirt.Example.Api.Connectors.Foo.Implementation` (`FooApiClient`). Mapping files live
under `wiremock/foo/mappings/`. Successful calls require header
`x-api-key: local-foo-api-key` (set in SSM path `/foo/api-key` from
`make-local-aws-resources.sh`). The API container reaches WireMock at
`http://wiremock-foo:8080`; from the host use `http://localhost:9100`.

| Method | Path                     | Auth                | Result                                                                          |
|--------|--------------------------|---------------------|---------------------------------------------------------------------------------|
| POST   | `/api/foo`               | valid `x-api-key`   | 200 with `{ "Id": <random int>, "Name": <request Name> }` (PascalCase JSON)     |
| GET    | `/api/foo/{id}`          | valid `x-api-key`   | 200 with `{ "Id": {id}, "Name": "Foo-{id}" }`                                   |
| GET    | `/api/foo/404`           | valid `x-api-key`   | 404 (exercises not-found handling)                                              |
| any    | `/api/foo` or `/api/foo/…` | missing/invalid key | 401                                                                           |
### Testing Unauthorized Behaviour

To conveniently set an invalid API key value in SSM, you can use the `foo-set-ssm-api-key.sh` script:

```bash
./foo-set-ssm-api-key.sh 'bogus-key-value-here'
```

Silly reminder: The API caches a successful key for its entire lifetime or until such a time as it becomes an unsuccessful key. This means that if the API instance is told to make a request to the Foo service and *then* a bad value is stored in SSM the API will have no reason to pull that bad key. This paragraph was made because of a local testing mix-up when the Foo example was first being developed (the hazards of developing code in one sitting and then local testing in the next). In order to force the pull of a bad key, you would need to use the below rotation script to update the key in WireMock and then manually.

### Testing Key Rotations

To conveniently set a new API key value in SSM *and* update WireMock's mappings (in-memory), use the `foo-rotate-api-key.sh` script.

```bash
./foo-rotate-api-key.sh
```

Confirming that this script will only update the in-memory versions that WireMock has loaded from disk. It will not adjust the mapping files mapped from the `wiremock/foo/` directory.

## Bar WireMock stubs

`wiremock-bar` mocks the Bar OAuth token endpoint and Bar HTTP API used by
`RedShirt.Example.Api.Connectors.Bar.Implementation` (`BarApiClient` +
`OAuthTokenSource`). Mapping files live under `wiremock/bar/mappings/`.

Default credentials (from `make-local-aws-resources.sh`):

- SSM `/bar/oauth/client-id` → `local-bar-client-id`
- SSM `/bar/oauth/client-secret` → `local-bar-client-secret`
- Access token returned by the token stub → `local-bar-access-token`

Compose points the API at `http://wiremock-bar:8080` for both `BaseUrl` and
`TokenUrl` (`…/oauth/token`), with scope form field `audience=https://bar.local/api`.
From the host use `http://localhost:9101`.

| Method | Path                       | Auth / body                                                              | Result                                                               |
|--------|----------------------------|--------------------------------------------------------------------------|----------------------------------------------------------------------|
| POST   | `/oauth/token`             | form: `grant_type=client_credentials`, valid client id/secret, audience  | 200 with `access_token` + `expires_in`                               |
| POST   | `/oauth/token`             | anything else                                                            | 401 `invalid_client`                                                 |
| POST   | `/api/bar`                 | `Authorization: Bearer local-bar-access-token`                           | 200 with `{ "Id": <random int>, "Name": <request Name> }`            |
| GET    | `/api/bar/{id}`            | valid Bearer                                                             | 200 with `{ "Id": {id}, "Name": "Bar-{id}" }`                        |
| GET    | `/api/bar/404`             | valid Bearer                                                             | 404 (exercises not-found handling)                                   |
| any    | `/api/bar` or `/api/bar/…` | missing/invalid Bearer                                                   | 401                                                                  |
### Testing Unauthorized Behaviour

To put an invalid client secret in SSM (token endpoint will 401 once credentials are refreshed):

```bash
./bar-set-ssm-oauth-secret.sh 'bogus-secret-value-here'
```

Same caching caveat as Foo: a successfully obtained bearer token stays cached until it fails or expires. Setting a bad secret in SSM alone does not invalidate an already-cached token. To force WireMock to reject the current token (and exercise refresh), use the rotation script below so the API's cached token no longer matches WireMock's Authorization matcher—or restart the API after changing secrets.

Local Compose defaults `COMMON__SECRETS__CACHE__FORCE_COOLDOWN_SECONDS` and
`CONNECTORS__BAR__TOKEN_REFRESH_COOLDOWN_SECONDS` to `1` so credential rotation can
recover on the next request. The rotate script waits briefly for those windows to
elapse before returning.

### Testing Credential / Token Rotations

To update the client secret in SSM *and* WireMock's in-memory stubs (token bodyPatterns, returned `access_token`, and API `Authorization` matchers):

```bash
./bar-rotate-oauth-credentials.sh
# or:
./bar-rotate-oauth-credentials.sh 'my-new-secret' 'my-new-access-token'
```

This only updates in-memory WireMock stubs. Restarting `wiremock-bar` restores the mapping files under `wiremock/bar/`. After the script finishes, call `GET /bar/{id}` or `POST /bar` again — the connector should 401 once with the old bearer, refresh client credentials + token, then succeed with the rotated bearer.
