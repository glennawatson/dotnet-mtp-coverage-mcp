# UnitTestMcp - Development Guide

## Project Overview

MCP server for analyzing code coverage from MTP-based .NET test projects. Parses Cobertura XML and exposes coverage data via MCP tools.

## Build & Test

```bash
# Build the solution
cd src && dotnet build

# Run all tests
cd src && dotnet test

# Run tests with coverage
cd src && dotnet test --collect:"XPlat Code Coverage"

# Run the MCP server locally
cd src && dotnet run --project UnitTestMcp.Server
```

## Project Layout

- `src/UnitTestMcp.Core/` - Core library (no MCP dependency). Models, Cobertura parser, coverage service.
- `src/UnitTestMcp.Server/` - MCP server entry point. Tool definitions in `Tools/CoverageTools.cs`.
- `src/tests/UnitTestMcp.Tests/` - TUnit tests with sample Cobertura XML in `TestData/`.

## Architecture

- **Models** (`UnitTestMcp.Core.Models`): Immutable records - `CoverageReport`, `PackageCoverage`, `ClassCoverage`, `MethodCoverage`, `LineCoverage`.
- **Parser** (`UnitTestMcp.Core.Parsers.CoberturaParser`): Static class using `GeneratedRegex`, parses Cobertura XML via `System.Xml.Linq`.
- **Service** (`UnitTestMcp.Core.Services.CoverageService`): Implements `ICoverageService` for querying coverage data.
- **MCP Tools** (`UnitTestMcp.Server.Tools.CoverageTools`): Static methods with `[McpServerTool]` attributes, auto-discovered by `WithToolsFromAssembly()`.

## Conventions

- .NET 10 only, C# latest (records, pattern matching, file-scoped namespaces, primary constructors, generated regex)
- Central package management via `Directory.Packages.props`
- StyleCop + Roslynator analyzers enforced
- TUnit test framework with MTP runner
- Copyright: Glenn Watson 2026, MIT license
- All public types must have XML documentation

## Adding New MCP Tools

1. Add a static method to `CoverageTools.cs` (or create a new `[McpServerToolType]` class in `Tools/`)
2. Annotate with `[McpServerTool]` and `[Description("...")]`
3. Use `ICoverageService` as a parameter (injected by DI)
4. Return a string (markdown-formatted for best LLM consumption)

## Adding New Coverage Queries

1. Add the method to `ICoverageService` interface
2. Implement in `CoverageService`
3. Add tests in `CoverageServiceTests.cs`
4. Optionally expose via MCP tool

## Test Data

Sample Cobertura XML fixtures are in `src/tests/UnitTestMcp.Tests/TestData/`. These are copied to the output directory at build time.
