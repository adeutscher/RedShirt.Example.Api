# RedShirt.Example.Api

Example of an ASP.NET Core API.

Repo features:

* Initialisation script for quick and convenient namespace adjustment.
* Use of [NSwag](https://github.com/RicoSuter/NSwag) to automatically document endpoints and to generate client code for
  an interop package.
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
* Example connectors to secondary APIs
    * The "Foo" connector connects to the imaginary Foo API using a static key.
    * The "Bar" connector connects to the imaginary Foo API using a bearer token obtained using a OAuth Client
      Credentials request.
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

# Authorization

JWT bearer authentication is optional (setting `AUTHENTICATION__DISABLE_AUTHENTICATION=true` disables it). When enabled,
Keycloak realm roles are mapped to `permission` claims. Endpoints authorize on those permissions, not on role names.

The flow of identity provider roles to actionable enforcement looks like this:

1. Identity provider provides a token. This token contains a series of claims. A claim is a key-value pairing, where the
   value could be a list.
2. The raw token claims are enriched by an implementation of `IClaimsTransformation`:
   `BespokeRolePermissionClaimsTransformation`. This enriches the client's claims with specific permissions.
    * The map of roles to permissions lives in `BespokeRolePermissionMap`.
3. In `AuthorizationServiceCollectionExtensions.AddApiAuthorizationPolicies`, policies are declared with requirements.
    * Many of these requirements are checking for a permission that is declared on the enriched series of claims.
    * Custom requirement can also be specified by providing implementations of `IAuthorizationRequirement`.
        * For example, this template's read policies are supplemented by a custom requirement that double-checks that
          the endpoint method to a GET endpoint.

The map of roles to permissions lives in `BespokeRolePermissionMap`. Role hierarchy belongs in the permission map (and
in Keycloak composites), not in authorization handlers.

## Constants

Authorization is heavily reliant on constants:

* `BespokeAuthorizationClaims`: Specify custom claim keys.
* `BespokeAuthorizationPermissions`: Specify permission names.
* `BespokeAuthorizationPolicies`: Specifies policy names.
* `BespokeAuthorizationRoles`: Specifies role names.

## Roles

This example template contains the following roles:

| Realm role  | Permissions                                                                                               | Access                                                            |
|-------------|-----------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------|
| `admin`     | `api:read`, `api:write`, `api:unrestricted`, `product:read`, `product:write`, `order:read`, `order:write` | All endpoints; bypasses customer resource scope                   |
| `developer` | Same as `admin` except without `api:unrestricted`                                                         | All endpoints; still limited by customer resource scope on orders |
| `analyst`   | `product:read`                                                                                            | GET `/products` only                                              |
| `billing`   | `order:read`, `order:write`                                                                               | Read-write `/orders`; still limited by customer resource scope    |

Locally, `admin` is a Keycloak composite that includes `developer`. The API map still grants the full set to `admin` so
authorization does not depend on the identity provider sending both role claims.

## Policies

* Fallback policy requires `api:write` (Foo, Bar, ExampleItem writes, and any undecorated action).
* `[ApproveReadOnly]` requires `api:read` on HTTP GET (Foo, Bar, ExampleItem).
* Product GET requires `product:read` on HTTP GET (`[ApproveProductReadOnly]`). Product writes require `product:write`.
* Order GET requires `order:read` on HTTP GET (`[ApproveOrderReadOnly]`). Order writes require `order:write`.
* **Orders** also use resource-based authorization: callers without `api:unrestricted` may only see orders whose
  `CustomerId` matches the JWT `customer_id` claim. Failed checks return **404** (same as a missing id) so existence is
  not leaked.

See `test/local/` for Keycloak users, token helper flags, and `curl` examples.

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

For local testing, see the `test/local/` directory. That guide covers bringing up MariaDB and applying schema updates
via
[RedShirt.Example.Schema](https://github.com/adeutscher/RedShirt.Example.Schema).

# Citations

Rough citation of some sources beyond memory.

## API

* https://github.com/RicoSuter/NSwag/issues/2409 - on models not showing up
* https://github.com/RicoSuter/NSwag/wiki/NSwag.MSBuild
* https://stackoverflow.com/questions/33283071/swagger-webapi-create-json-on-build
* https://github.com/RicoSuter/NSwag/wiki/CommandLine/ce950c5aea7bf52a85ec6e517ad8ea96762181ed
* https://github.com/RicoSuter/NSwag/issues/1573
    * Should use aspnetcore2swagger
* https://github.com/RicoSuter/NSwag/issues/3119
    * Use nobuild to avoid infinite build loop
* https://github.com/RicoSuter/NSwag/wiki/NSwag-Configuration-Document
    * Doesn't include sourcing from an existing swagger.json though...
    * Derived 'Net80' runtime from phrasing in EXE variable
* https://stackoverflow.com/questions/63791017/generate-with-nswag-an-openapi-document-including-swashbuckle-custom-operation-f
    * Describes fromDocument section
    * Derive from Url, but we want path
* https://stackoverflow.com/questions/73016248/generating-c-sharp-api-client-with-nswag-msbuild\
    * Derive from Json
* https://stackoverflow.com/questions/59393267/generate-nswag-client-as-part-of-the-build
* Mentions rigging up before build
* https://github.com/RicoSuter/NSwag/wiki/AspNetCoreOpenApiDocumentGenerator
* https://github.com/RicoSuter/NSwag/blob/master/src/NSwag.Commands/Commands/Generation/AspNetCore/AspNetCoreToOpenApiCommand.cs
* https://github.com/RicoSuter/NSwag/blob/master/src/NSwag.Commands/Commands/OutputCommandBase.cs

## Dynamo

* https://codewithmukesh.com/blog/pagination-in-amazon-dynamodb-with-dotnet/
