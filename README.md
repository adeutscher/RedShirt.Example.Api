# RedShirt.Example.Api

Forkable ASP.NET Core API template: rename the namespace, keep the scaffolding. Includes JWT authorization, optional
Roslyn-generated data access, file uploads, OpenAPI clients via NSwag, and a local Docker Compose stack.

# Template

## Template Philosophy

The central philosophy of this template is flexibility and preparedness. A template maintained with lessons from past
projects can provide a stable foundation from which to launch future projects. The use of a template is more sustainable
and resource-efficient than adapting directly from past projects.

This template on its own will likely be more feature-rich than any one project needs. Because of this, this template is
designed to make it convenient to prune away unused components. It is much easier to delete an unneeded component than
it is to generate a new component.

## Features

Repo features in more detail:

* Initialisation script for quick and convenient namespace adjustment.
* Use of [NSwag](https://github.com/RicoSuter/NSwag) to automatically document endpoints to OpenAPI standard and to
  generate client code for an interop package.
    * Recommended next step: Exporting the interop package as a NuGet for use in other projects.
* [Roslyn](https://github.com/dotnet/roslyn) source generation for MariaDB/Dapper data-access scaffolding (services,
  repositories, search requests, and related DI) from annotated DTO models.
    * This is demonstrated in the implementation for accessing the `Order` data store.
    * For more information on source generation,
      see [docs/patterns/rosalyn-source-generation.md](docs/patterns/rosalyn-source-generation.md).
* A scaffolding tool (`scripts/scaffold.sh`) that writes the controller, CQRS use cases, request models, authorization
  wiring, and test skeletons for a data store resource as ordinary source files for review and customization.
    * For more information, see [docs/patterns/scaffolding.md](docs/patterns/scaffolding.md).
* Configurable rate limiting using a sliding window system:
    * Uses either Redis or in-memory for storing limits.
* JWT bearer authentication and role-based authorization (optional; Keycloak in the local Compose stack).
    * Realm roles (`admin`, `developer`, `analyst`, `billing`) map to permission claims; endpoints authorize on those
      permissions, not on role names.
    * Resource-based authorization on orders: callers without `api:unrestricted` (`admin`) may only access rows whose
      `CustomerId` matches the JWT `customer_id` claim.
* Example connectors to secondary APIs:
    * The "Foo" connector connects to the imaginary Foo API using a static key.
    * The "Bar" connector connects to the imaginary Foo API using a bearer token obtained using a OAuth Client
      Credentials request.
    * Both connectors support key rotation: Credentials are refreshed out of the chosen secret manager if/when the
      current credentials cease working, allowing for keys to be rotated without restarting the application. Key
      rotation on both connectors is throttled by a refresh cooldown parameter preventing bad credentials in the secret
      manager from being continually polled.
    * Both connectors honour retry hints: When the dependency throttles a call (HTTP 429) or is unavailable (HTTP 503)
      and names a wait, the connector waits exactly that long before retrying. Other transient failures use exponential
      backoff with jitter. A failure that outlasts the retries is thrown as `BarConnectorException` or
      `FooConnectorException` with `IsThrottled` and `RetryAfter` set, so callers can back off themselves.
    * Retry settings per connector (`Connectors__Bar__*` or `Connectors__Foo__*`):
        * `RetryCount` (default 3): Retries per call, shared by every transient failure.
        * `MaxRetryAfterSeconds` (default 30): Longest hint waited out. A longer hint fails the call at once. 0 or less
          accepts any hint.
        * `RetryThrottledPerpetually` (default false): Keep retrying a throttled call with a hint without using up
          `RetryCount`. Only the caller's cancellation token ends it.
    * Connection settings per connector, on the same `Connectors__Bar__*` or `Connectors__Foo__*` keys:
        * `ClientPerRequest` (default false): A new client and connection per attempt. False uses the connector's
          pooled client from `IHttpClientFactory`, which reuses connections across calls.
        * `HandlerLifetimeSeconds` (default 120): Seconds before the pooled handler is replaced, which picks up DNS
          changes. At least 1. Pooled only.
        * `MaxConnectionsPerServer` (default 0): Cap on open connections. Requests over the cap wait for a free
          connection, and the wait counts against the request timeout. 0 or less is no cap. Pooled only.
        * "Pooled only" settings are ignored when `ClientPerRequest` is true. The Bar token endpoint keeps its own
          pooled client whatever `ClientPerRequest` says.
* Configuration is based on environment variables.
* Hardened HTTP responses: security headers on every response, and host filtering with a startup warning while any
  host is allowed.
    * For more information, see [`docs/patterns/hosting.md`](docs/patterns/hosting.md).
* File upload and tracking examples.
* Server-sent events over MQTT.
    * For more information, see [`docs/patterns/server-sent-events.md`](docs/patterns/server-sent-events.md)
* Documentation on common data handling practices:
    * [Event Sourcing](docs/patterns/event-sourcing.md)

### Objects

This project provides several different examples of data storage techniques:

* `ExampleItem` stores data in a DynamoDB table.
* `Order` stores data in a MariaDB table, accessed via Dapper with implementations generated by Rosalyn source
  generation.
    * This system was designed to rapidly generate access clients for basic CRUD tables.
* `Product` stores data in a MariaDB table, accessed via Dapper.
    * This example is directly adapted from the source-generated code.
* `Customer` stores data in a MariaDB table, accessed via Entity Framework.

### Uploads

This template provides a system for accepting, tracking, and validating file uploads.

For more information on the uploads system, see [docs/patterns/uploads.md](docs/patterns/uploads.md).

#### Configuration

Configuration environment variables for the upload system.

| Setting                          | Environment variable                      |
|----------------------------------|-------------------------------------------|
| Max request body size (nullable) | `UPLOADS__MAX_UPLOAD_SIZE_BYTES`          |
| Unverified bucket                | `UPLOADS__BUCKET_UNVERIFIED_ITEMS`        |
| Verified bucket                  | `UPLOADS__BUCKET_VERIFIED_ITEMS`          |
| Presigned URL lifetime (minutes) | `UPLOADS__PRESIGNED_URL_LIFETIME_MINUTES` |

### Related: Schema

Database DDL for this API lives in a separate repository:
[RedShirt.Example.Schema](https://github.com/adeutscher/RedShirt.Example.Schema).

The schema project owns MariaDB/MySQL schema versioning (using the DbUp library to apply incremental SQL scripts). This
API assumes those tables already exist and does not create or migrate them. The intent of having a dedicated schema
project is to enforce separation of concerns. The database credentials used by the API should not have the power to
affect the schema on a fundamental level. When developing against the local Compose stack, apply schema updates from the
Schema repo before starting the API (see `test/local/`).

# Initialisation

Below are the recommended steps for using this as a template:

1. To change the namespace of the API en-masse for your purposes, use the `init-repo.sh` script:

    ```bash
    bash init-repo.sh New.Namespace.Here
    ```

2. Write database accessors and/or service connectors based on your application's needs.
3. Prune database accessors and/or service connectors that your application does not need.
4. The example of Server-Sent Events needed a bespoke handler, the standard NSwag clients did not know how to support
   event streams. This handler can be found in the `RedShirt.Example.Api.Interop` project, defined in
   `EventStreamListener.cs`.
    * The `EventStreamListener.cs` contains the abstract `EventStreamListener` and the applied
      `MessagesEventStreamListener`, which sets some defaults.
    * If you are implementing Server-Sent Events but not as `/messages` as this example does, then I would recommend
      adjusting the comments described in `MessagesEventStreamListener`.
    * If you are not implementing Server-Side Events in your applied application, then I would recommend deleting
      `EventStreamListener.cs` outright.
        * The pattern of emulating the generated swagger clients by throwing a `SwaggerException` required the manual
          declaration of `SwaggerException` and `ProblemDetails` to avoid a chicken-and-egg problem on build where a
          cold build without generated code failed because the manual code was expecting it. If you are removing
          `EventStreamListener`, then you might also want to consider removing these manual classes and adjusting
          `nswag.json` to generate them again.
5. Consider revising/pruning the Markdown files such as this README or those located in the `docs/` directory. They
   heavily assume that they are speaking for a general template and not for an applied application.
6. Consider carving off some common project into separate NuGets, especially if you are planning to use this template
   multiple times. This template doesn't any of my NuGet packages because it's meant to be a transparent public
   template, but in an applied project that thinking doesn't apply.
    * I've put more of my thoughts on NuGet packaging here in [nuget-packaging.md](docs/patterns/nuget-packaging.md)

## Secret Manager

Many components of this template rely on an implementation of the `ISecretManagerService` interface to function. Use of
a secret manager is highly encouraged for values that could have sensitive information such as credentials to external
services.

See [Secret Manager](./docs/patterns/secret-manager.md) for more information.

## Background Services

In addition to acting as an API, this application can also run background services by making an implementation of
`BackgroundService` and referencing it during setup. That said, I would advise considering an external process for
background tasks at scale.

See [Background Events](./docs/patterns/background-events.md) for more details.

# Configuration

This API is expected to be run out of a docker container, so it relies on environment variables for most of its
configuration.

For configuration examples, see the `api` section of the `test/local/docker-compose.yaml` file.

## Path Base

When the API is hosted under a URL prefix (for example behind a reverse proxy at `/example`), set
`API__PATH_BASE` to that prefix (for example `/example`) so that routing and link generation treat requests relative to
that base. Leave it unset (or null, or blank) when the app is served at the site root.

## Rate Limiting Configuration

This API is built with the option for rate limiting, using a sliding window system backed either by Redis or an
in-memory system.

To make use of rate limiting, you must either:

* Use the `[EnableRateLimiting("example")]` attribute to name a policy (unlike the example on this list item, using
  constants for this is strongly encouraged).
* Set and configure a default policy to require rate limiting across all endpoints (unless ruled out by the use of the
  `[DisableRateLimiting]` attribute applied to an endpoint or controller).

Resources:

* For configuration examples, see the `api` section of the `test/local/docker-compose.yaml` file. Rate limiting is
  defined in environment variables beginning in `RATE_LIMITING`.
* To better understand the configuration definitions, refer to the classes in the `Configuration/` folder of the
  `Common.RateLimiting` project

# Development

Tips for local development.

## Debugging Source Generation

See [Debugging source generation](docs/patterns/rosalyn-source-generation.md#debugging).

# Testing

For local testing, see the `test/local` folder. That guide covers bringing up MariaDB and applying schema updates via
[RedShirt.Example.Schema](https://github.com/adeutscher/RedShirt.Example.Schema).