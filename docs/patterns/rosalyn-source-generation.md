# Rosalyn Source Generation

This document describes the standards and decisions made around the use of Rosalyn Source Generators.

## History

The use of source generation came about when I faced the following project constraints:

* I was facing a large-scale personal project.
* As the only developer, my resources were limited.
* The project was very data-driven and needed CRUD endpoints for a large number of tables.
* APIs could in theory support the main application and support applications that populate or manipulate data while
  maintaining a principle of single ownership over a table.
* I didn't want to revisit tables for fundamental technical debt.
* I wanted my database tables classes to be as consistent as possible.
* The project was very experimental, so database tables were experimental as well.

With these constraints, I needed a solution that massively cut down on implementation turnaround time. I needed to get
my turnaround time to the point where I wouldn't feel like I wasted my time and resources if I decided the next day to
not use a particular table at all. I had already developed a series of generic handlers for handling simple CRUD
operations, but adding search functionality was still a lengthy task with lots of fiddly bits to get wrong by accident.

I settled on a system of using Source Generation to generate the Service and Repository layers of accessing a table.
Rosalyn Source Generators were a good fit because of their deterministic nature. Simple tables could be maintained
according to a consistent standard. Endpoint/CQRS-level implementation of endpoints remained a separate task because of
their greater chance of having bespoke rules per table.

## Debugging

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