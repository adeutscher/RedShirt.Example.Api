# Scaffolding

This document describes the scaffolding tool. The scaffolding tool is a convenience utility
which writes the API controller, CQRS use cases, request models, authorization wiring,
and test skeletons for a data store resource.

## Why scaffolding and not source generation

[Rosalyn source generation](rosalyn-source-generation.md) owns the data store layer: services, repositories, and
search requests are regenerated on every build and are never edited by hand. The layers above it are different.
Controllers and use cases tend to pick up bespoke authorization, validation, and mapping over time, so they need to be
ordinary source files that can be reviewed and changed case by case.

The scaffolding tool fills that gap. It writes literal `.cs` files into `RedShirt.Example.Api`,
`RedShirt.Example.Api.Core`, and their unit test projects. Once written, the files belong to you. The tool never
overwrites a file that already exists, so running it again is safe.

The tool is deterministic. The same data store and options always produce the same files, without spending any LLM
tokens.

## What it reads

The tool reads the data store's service contract with Roslyn (`MSBuildWorkspace`), so source-generated types are
included. For a resource named `Order` it looks for:

* `IOrderService`, declared in the data store project.
* The DTO, from the return type of `GetByIdAsync` (`OrderDto`).
* The request types, from the first parameter of `PostAsync`, `PutAsync`, `PatchAsync`, and `SearchAsync`.
* The key, from the first parameter of `GetByIdAsync`. Its name comes from the first property of the patch (or put)
  request, which is `Id` for generated data stores.
* The search response and continuation token type, from `SearchAsync`.
* One public `IServiceCollection` extension method in the registration project (`AddOrders`).

The request types already describe the exact shape the layers above them need: which search filters exist, which
fields are nullable, and which patch fields have a `Clear` flag. Reading them means the scaffolder never repeats the
source generator's rules and cannot drift from them.

Data store projects are found by convention under `src/`:

* `RedShirt.Example.Api.DataStores.<Resource>`, a single project like `Order` (the source-generated shape).
* `RedShirt.Example.Api.DataStores.<Resource>.Core` and `.Implementation`, a pair like `Product`. The contract is read
  from `.Core` and the registration from `.Implementation`.

Pass `--data-store` (and `--registration-project`) for anything else.

## What it writes

Use cases are written under `src/RedShirt.Example.Api.Core/UseCases/<Resource>/`. Each use case has a request record,
a handler, and a validator. A use case is skipped when the service has no matching method (for example, there is no
`Create` for a DTO marked `[DoNotGeneratePost]`).

| Use case                | Service method | Controller action      |
| ----------------------- | -------------- | ---------------------- |
| `Commands/Create`       | `PostAsync`    | `POST /<route>`        |
| `Commands/Delete`       | `DeleteAsync`  | `DELETE /<route>/{id}` |
| `Commands/Patch`        | `PatchAsync`   | `PATCH /<route>/{id}`  |
| `Commands/Update`       | `PutAsync`     | `PUT /<route>/{id}`    |
| `Queries/GetRecord`     | `GetByIdAsync` | `GET /<route>/{id}`    |
| `Queries/SearchRecords` | `SearchAsync`  | `GET /<route>`         |

The tool also writes:

* `Controllers/<Resource>Controller.cs` in the API project, following `OrderController`.
* Request models under `Models/<Resource>/` (`PostRequest`, `PutRequest`, `PatchRequest`, `SearchRequest`).
* `Approve<Resource>ReadOnlyAttribute` and `Authorize<Resource>WriteAttribute` under `Attributes/Authorization/`.
* Handler tests for every use case, plus a `<Resource>TestData` helper, in `RedShirt.Example.Api.Core.UnitTests`.
* Validator tests where the validator has rules (a non-empty `Guid` key, or required strings on create and update).
* Controller tests in `RedShirt.Example.Api.UnitTests`.

Validators start with the same rules the Order use cases have: the key must not be `Guid.Empty`, and non-nullable
strings must not be blank on create and update. Add bespoke rules by hand.

### Shared files

Wiring that has to live in existing files is inserted just above anchor comments such as `// scaffold:permissions`.

| Anchor                                | File                                                                    | Inserted content                                         |
| ------------------------------------- | ----------------------------------------------------------------------- | -------------------------------------------------------- |
| `permissions`                         | `Authorization/Constants/BespokeAuthorizationPermissions.cs`            | `<Resource>Read` and `<Resource>Write` constants         |
| `policies`                            | `Authorization/Constants/BespokeAuthorizationPolicies.cs`               | `<Resource>Write` and `<Resource>ReadApproved` constants |
| `policy-registrations`                | `Extensions/AuthorizationServiceCollectionExtensions.cs`                | Two `AddPolicy` calls                                    |
| `full-access-permissions`             | `Authorization/BespokeRolePermissionMap.cs`                             | Both permissions for the full-access roles               |
| `data-store-usings`                   | `Extensions/ServiceCollectionExtensions.cs`                             | `using` for the data store registration                  |
| `data-store-registrations`            | `Extensions/ServiceCollectionExtensions.cs`                             | The data store registration call                         |
| `test-full-access-permissions`        | `Tests/Authorization/BespokeRolePermissionMapTests.cs`                  | Both permissions                                         |
| `test-admin-permissions`              | `Tests/Authorization/BespokeRolePermissionMapTests.cs`                  | Both permissions                                         |
| `test-claims-full-access-permissions` | `Tests/Authorization/BespokeRolePermissionClaimsTransformationTests.cs` | Both permissions                                         |

Each snippet is matched by its exact text, so the tool can tell whether it is already present (create skips it) and
find it again later (remove deletes it). An anchor only owns the lines between itself and the anchor above it, which
keeps identical snippets for two anchors in the same file apart.

Only the full-access roles get the new permissions. Granting them to other roles (such as `billing`) is a business
decision, so do that by hand in `BespokeRolePermissionMap`.

The tool also adds a `ProjectReference` to the data store from the Core and API projects, as a single line in the same
style as the existing ones.

## Running scaffolding

The recommended entrypoint is `scripts/scaffold.sh`. It builds the data store project first, so source-generated
types are up to date, and then runs the tool.

```bash
scripts/scaffold.sh create Order --dry-run
```

```bash
scripts/scaffold.sh create Order --scope customer --manifest artifacts/scaffold/order.manifest
```

As it runs, the tool announces every file it touches:

```text
created        src/RedShirt.Example.Api.Core/UseCases/Order/Commands/Create/CreateOrderCommand.cs
skipped        src/RedShirt.Example.Api/Controllers/OrderController.cs (exists)
modified       src/RedShirt.Example.Api/Authorization/Constants/BespokeAuthorizationPermissions.cs (permissions)
```

With `--dry-run`, the same lines start with `would create`, `would modify`, or `would remove`, and nothing is written.

Before create writes anything, it checks that every anchor exists exactly once. If one is missing, it lists the
problems and changes nothing.

After scaffolding, build and test, then review the new files like any other change:

```bash
dotnet test
```

Building the API also regenerates `swagger/RedShirt.Example.Api.Interop/swagger.json` with the new endpoints.

### Options

| Option                            | Mode   | Description                                                           |
| --------------------------------- | ------ | --------------------------------------------------------------------- |
| `--data-store <csproj>`           | both   | Project declaring `I<Resource>Service`. Default: found by convention. |
| `--registration-project <csproj>` | both   | Project declaring the `IServiceCollection` registration.              |
| `--registration <name>`           | both   | Registration method name, when the project has more than one.         |
| `--route <route>`                 | both   | Controller route. Default: kebab-case plural (`beans-widgets`).       |
| `--scope customer`                | both   | Order-style customer resource scoping in the controller.              |
| `--manifest <file>`               | both   | Write the paths created, modified, or removed to `<file>`.            |
| `--dry-run`                       | both   | Report what would change without writing anything.                    |
| `--force`                         | remove | Also delete files edited since scaffolding.                           |
| `-y`, `--yes`                     | remove | Skip the confirmation prompt.                                         |
| `--repo-root <dir>`               | both   | Repository root. The script always passes this.                       |
| `--no-build`                      | both   | Script only. Skip building the data store project(s) first.           |

### Manifest

`--manifest <file>` writes the repository-relative paths that were created, modified, or removed during the run, one per
line. Skipped files are left out. `artifacts/` is ignored by git, which makes `artifacts/scaffold/` a convenient place
for manifests. A manifest is plain text, so it works with tools like `xargs`:

```bash
xargs git add < artifacts/scaffold/order.manifest
```

### Removing a resource

`remove` undoes `create`. Pass the same `--route` and `--scope` that were given to `create`, because the files are
compared with what those options would produce.

```bash
scripts/scaffold.sh remove Order --scope customer
```

The tool lists everything it would remove, then asks you to type the resource name (here `Order`) to confirm. Anything
else cancels with nothing changed. `-y` skips the prompt. Without `-y`, remove refuses to run when there is no terminal
to confirm on (for example in CI), rather than waiting for input.

Remove is careful about your changes:

* A file is only deleted when it still matches the template output exactly. Edited files are reported as
  `kept (modified since scaffolding, use --force)`.
* A shared-file snippet is only deleted when its exact text is still there. An edited snippet is reported as a
  warning, and you remove it by hand.
* `-y` and `--force` are separate. `remove -y` skips the question but still keeps your edits. `--force` also deletes
  edited files.
* Empty use case and model directories are deleted.

Run remove before deleting the data store project. Without the data store, the tool cannot render the files to compare
them, so it keeps them unless you pass `--force`, and the data store `using` and registration need removing by hand.

After removing, build and test to find anything else that still refers to the resource, such as code you wrote by hand.

### Running the project directly

`scripts/scaffold.sh` is a thin wrapper. You can run the tool with `dotnet run` instead, as long as you build the data
store project first and tell the tool where the repository is (it searches upward from the working directory
otherwise):

```bash
dotnet build src/RedShirt.Example.Api.DataStores.Order
```

```bash
dotnet run --project tools/RedShirt.Example.Api.Scaffolding -- create Order --scope customer --repo-root .
```

Everything after `--` goes to the tool. `--help` prints the full usage.

## Customizing the templates

Templates are [Scriban](https://github.com/scriban/scriban) files under `tools/RedShirt.Example.Api.Scaffolding/Templates/`:

* `core/<UseCase>/`: the request record, handler, and validator for each use case.
* `api/`: the controller, request models, and authorization attributes.
* `tests/`: test skeletons.
* `snippets/`: the content inserted at each anchor. A snippet is written at column 0 and indented to match its anchor.

Each template sees a `model` object with snake_case members, such as `model.name`, `model.dto_type`,
`model.search_properties`, and `model.customer_scope`. See `Rendering/ResourceModel.cs` for the full list. A `usings`
function turns a list of namespaces into sorted `using` lines.

Changing a template only affects files scaffolded afterwards. Because remove compares files with the current
templates, files scaffolded before a template change count as modified.

## Adding an anchor

To insert into another shared file:

* Add a `// scaffold:<name>` comment where new lines should go. The lines above it, up to the previous anchor, are the
  region the tool searches.
* Add a snippet template under `Templates/snippets/`.
* Add a `Snippet(...)` entry in `Planning/ScaffoldPlanner.cs`.

A list that new entries are appended to needs a trailing comma after its last item, so the anchor can sit below it.
C# collection expressions allow this.

## Known limitations

* `--scope customer` mirrors `OrderController`. `Post` does not check the `CustomerId` in the request, and `Put` and
  `Patch` check the existing record's customer but not a new `CustomerId`. Review this for each resource.
* Search handlers call the request validator, which the Order and Product search handlers do not.
* The data store `using` is added at the end of the `using` block, not in sorted order.
* Project references are only recognised when the `<ProjectReference .../>` element is on a single line.
* Data store limits show up as build errors in the data store, not in the scaffolder. At the time of writing, the
  source generator rejects enum properties, and `[RangeSearchable]` produces a search request that does not compile.

## Tests for the tool

The tool and its unit tests live in their own solution, `tools/scaffolding.slnx`, which keeps their Roslyn and MSBuild
dependencies out of the API's Docker build.

```bash
dotnet test --project tools/RedShirt.Example.Api.Scaffolding.UnitTests
```
