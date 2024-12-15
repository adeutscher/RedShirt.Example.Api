# RedShirt.Example.Api

# Initialisation

To change the namespace of the API en-masse for your purposes, use the `init-repo.sh` script:

```bash
bash init-repo.sh New.Namespace.Here
```

# Testing

For local testing, see the `test/local` folder.

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
