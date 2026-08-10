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

1. Bring up `ministack`, `redis`, `mariadb`, and `wiremock-foo` containers:

    ```bash
    docker compose up -d ministack redis mariadb wiremock-foo
    ```

    See **Foo WireMock stubs** below for the mocked endpoints. Admin UI:
    http://localhost:9100/__admin/

2. Run `make-local-aws-resources.sh` to create ministack resources (DynamoDB table and
   SSM parameters, including `/mysql/connection-string` and `/foo/api-key`):

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
`x-api-key: local-foo-api-key` (Set in SSM path `/foo/api-key` from
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
