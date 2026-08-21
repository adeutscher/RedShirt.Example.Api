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

## Secret Manager

Many components of this template rely on an implementation of the `ISecretManagerService` interface to function. Use of
a secret manager is highly encouraged for values that could have sensitive information such as credentials to external
services.

Currently, the template provides 3 possible implementations:

* [AWS SSM Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html)
* [Azure Key Vault](https://azure.microsoft.com/en-us/products/key-vault)
* [Docker Secrets](https://docs.docker.com/reference/compose-file/secrets/)
  ([Secondary Link](https://docs.docker.com/engine/swarm/secrets/))

### Docker Secrets

While the other secret managers are more straightforward with their plans, Docker Secrets has some caveats and
assumptions that should be documented.

In general, I would encourage the use of a secret manager other than Docker Secrets. Compared to other options it lacks
flexibility. However, it may be exactly what is needed for a small-scale environment.

Other notes:

* Unlike other secret managers, a container instance's secrets cannot be rotated without restarting the
* Secrets are assumed to be files in `/run/secrets`
    * This directory can be overridden by setting a new path in `COMMON__SECRETS__DOCKER__DIRECTORY`
* Secret keys are assumed to be roughly equal to the underlying file specified in the Docker stack configuration. If the
  file does not meet these guidelines and because the compose file specifies another target path within the container,
  then the secret manager has nothing with which to resolve a key to a file containing a value. After checking the exact
  name of the key under the secret directory, the secret manager will attempt the path with a few file extensions
  (though realistically, only the flat key match will probably be useful).
    * For example, with no overriding directory the key `foo-password` will be searched for under the following absolute
      paths:
        * `/run/secrets/foo-password`
        * `/run/secrets/foo-password.txt`
        * `/run/secrets/foo-password.json`
    * In general, it's advised not to meddle with secret targets within the container at all if you plan to use them
      with this template's Docker secret manager.
* The implementation has not been tested under Docker Swarm, and as such hasn't been tested with external secrets.

## Background Services

In addition to acting as an API, this application can also run background services.

To do so, you can do the following:

1. Declare class that inherits from `BackgroundService`

    ```csharp
    public sealed class ExampleHostedService(IStuffDoerService stuffDoerService) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Perform action. It's assumed that the implementation runs continually.
                await stuffDoerService.DoStuffAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }

    ```

2. Add a hosted service when setting up your service collection:

    ```csharp
    services.AddHostedService<ExampleHostedService>()
    ```

Doing this could be useful for:

* Internal maintenance within the scope of the API.
* When there is a single instance of the API, it could be used for broader app maintenance such as periodically invoking
  CQRS handlers.
    * This case is an infrastructure shortcut to avoid the overhead of creating/maintaining a separate worker
      application for simple tasks. This option should not be explored or extracted out into a worker application the
      moment the environment plan involves multiple instances of the API running in an environment.

This a standard feature of a hosted .NET application and not a special feature of this template, but I figured that it
was worth documenting here.

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