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

1. Bring up `ministack`, `redis`, `mariadb`, `wiremock-foo`, and `wiremock-bar` containers:

    ```bash
    docker compose up -d ministack redis mariadb wiremock-foo wiremock-bar
    ```

    See **Foo WireMock stubs** and **Bar WireMock stubs** below for the mocked
    endpoints. Admin UIs:
    http://localhost:9100/__admin/ (Foo),
    http://localhost:9101/__admin/ (Bar).

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

## Foo WireMock stubs

`wiremock-foo` mocks the external Foo HTTP API used by
`RedShirt.Api.Example.Connectors.Foo.Implementation` (`FooApiClient`). Mapping files live
under `wiremock/foo/mappings/`. Successful calls require header
`x-api-key: local-foo-api-key` (set in SSM path `/foo/api-key` from
`make-local-aws-resources.sh`). The API container reaches WireMock at
`http://wiremock-foo:8080`; from the host use `http://localhost:9100`.

```
| Method | Path                     | Auth                | Result                                                                 |
|--------|--------------------------|---------------------|------------------------------------------------------------------------|
| POST   | /api/foo                 | valid x-api-key     | 200 with { "Id": <random int>, "Name": <request Name> }                |
|        |                          |                     | (PascalCase JSON, matching the connector DTOs)                         |
| GET    | /api/foo/{id}            | valid x-api-key     | 200 with { "Id": {id}, "Name": "Foo-{id}" }                            |
| GET    | /api/foo/404             | valid x-api-key     | 404 (exercises not-found handling)                                     |
| any    | /api/foo or /api/foo/... | missing/invalid key | 401                                                                    |
```

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
`RedShirt.Api.Example.Connectors.Bar.Implementation` (`BarApiClient` +
`OAuthTokenSource`). Mapping files live under `wiremock/bar/mappings/`.

Default credentials (from `make-local-aws-resources.sh`):

- SSM `/bar/oauth/client-id` → `local-bar-client-id`
- SSM `/bar/oauth/client-secret` → `local-bar-client-secret`
- Access token returned by the token stub → `local-bar-access-token`

Compose points the API at `http://wiremock-bar:8080` for both `BaseUrl` and
`TokenUrl` (`…/oauth/token`), with scope form field `audience=https://bar.local/api`.
From the host use `http://localhost:9101`.

```
| Method | Path                     | Auth / body                                              | Result                                                                 |
|--------|--------------------------|----------------------------------------------------------|------------------------------------------------------------------------|
| POST   | /oauth/token             | form: grant_type=client_credentials, valid client_id/secret, audience | 200 with access_token + expires_in                            |
| POST   | /oauth/token             | anything else                                            | 401 invalid_client                                                     |
| POST   | /api/bar                 | Authorization: Bearer local-bar-access-token             | 200 with { "Id": <random int>, "Name": <request Name> }                |
| GET    | /api/bar/{id}            | valid Bearer                                             | 200 with { "Id": {id}, "Name": "Bar-{id}" }                            |
| GET    | /api/bar/404             | valid Bearer                                             | 404 (exercises not-found handling)                                     |
| any    | /api/bar or /api/bar/... | missing/invalid Bearer                                   | 401                                                                    |
```

### Testing Unauthorized Behaviour

To put an invalid client secret in SSM (token endpoint will 401 once credentials are refreshed):

```bash
./bar-set-ssm-oauth-secret.sh 'bogus-secret-value-here'
```

Same caching caveat as Foo: a successfully obtained bearer token stays cached until it fails or expires. Setting a bad secret in SSM alone does not invalidate an already-cached token. To force WireMock to reject the current token (and exercise refresh), use the rotation script below so the API's cached token no longer matches WireMock's Authorization matcher—or restart the API after changing secrets.

### Testing Credential / Token Rotations

To update the client secret in SSM *and* WireMock's in-memory stubs (token bodyPatterns, returned `access_token`, and API `Authorization` matchers):

```bash
./bar-rotate-oauth-credentials.sh
# or:
./bar-rotate-oauth-credentials.sh 'my-new-secret' 'my-new-access-token'
```

This only updates in-memory WireMock stubs. Restarting `wiremock-bar` restores the mapping files under `wiremock/bar/`.
