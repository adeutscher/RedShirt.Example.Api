# RedShirt.Example.Api

Forkable ASP.NET Core API template: rename the namespace, keep the scaffolding. Includes JWT authorization,
Roslyn-generated data access, OpenAPI clients via NSwag, and a local Docker Compose stack.

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
* Configuration is based on environment variables.

## Related: Schema

Database DDL for this API lives in a separate repository:
[RedShirt.Example.Schema](https://github.com/adeutscher/RedShirt.Example.Schema).

That project owns MariaDB/MySQL schema versioning (using the DbUp library to apply incremental SQL scripts). This API
assumes those tables already exist and does not create or migrate them. The intent of this dedicated schema was to
enforce separation of concerns and prevent the API from having the power to affect the schema on a fundamental level.
When developing against the local Compose stack, apply schema updates from the Schema repo before starting the API (see
`test/local/`).

# Initialisation

To change the namespace of the API en-masse for your purposes, use the `init-repo.sh` script:

```bash
bash init-repo.sh New.Namespace.Here
```

# Configuration

This API is expected to be run out of a docker container, so it relies on environment variables for most of its
configuration.

For configuration examples, see the `api` section of the `test/local/docker-compose.yaml` file.

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

If you are developing new features for source generation, you may find that the standard build for solution or the
ASP.NET subproject does not express errors in the generation very well. Generally, it shall only print the exception
message with no further context.

The way around this is to print out the compiler's SARIF logs:

```bash
dotnet build src/RedShirt.Example.Api.Implementations.Orders/RedShirt.Example.Api.Implementations.Orders.csproj \
  /p:ErrorLog=compiler-diagnostics.sarif.log
find . -name '*sarif.log'
```

The stack trace should be in the logs for the project that you targeted:

```bash
less ./src/RedShirt.Example.Api.Implementations.Orders/compiler-diagnostics.sarif.log
```

If the build does not show up, run `dotnet clean` to ensure a fresh build:

```bash
dotnet clean
```

## Debugging Source Generation Not Appearing (Rider)

Generated files typically show up in a C# project under **Dependencies / .NET <VERSION> / Source Generators**. If this
**Source Generators** folder is not showing up and the source generator phase of the build appears to be working, then
you may need to click the UI button for **Restart Roslyn Analyzers and Source Generators**. In JetBrains Rider, it can
be found at It can be found as an item in the Rosalyn Analyzers menu in the bottom-right of the main window. I can only
describe the Rosalyn logo as "a weird branch-y thing".

# Testing

For local testing, see the `test/local` folder. That guide covers bringing up MariaDB and applying schema updates via
[RedShirt.Example.Schema](https://github.com/adeutscher/RedShirt.Example.Schema).