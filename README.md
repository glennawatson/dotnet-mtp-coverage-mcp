# UnitTestMcp

An MCP (Model Context Protocol) server for analyzing code coverage from MTP (Microsoft Testing Platform) based .NET test projects. Parses Cobertura XML coverage reports and exposes coverage data through MCP tools for AI-assisted development workflows.

## Features

- Parse Cobertura XML coverage reports (the standard output from `dotnet test` with code coverage)
- Query missed lines and branches for individual source files
- Get overall coverage for classes, methods, projects, and entire solutions
- Generate comprehensive missed coverage reports across a solution
- Discover and merge multiple coverage reports automatically

## Prerequisites

- [.NET 10 SDK](https://dot.net) or later
- An MTP-compatible test framework (e.g., [TUnit](https://github.com/thomhurst/TUnit), MSTest, xUnit v3)
- Code coverage collection configured (via `testconfig.json` or CLI flags)

## Installation

```bash
dotnet tool install --global UnitTestMcp.Server
```

Or run directly from source:

```bash
cd src
dotnet run --project UnitTestMcp.Server
```

## MCP Tools

| Tool | Description |
|------|-------------|
| `GetMissedCoverageForFile` | Gets missed lines and branches for a specific source file |
| `GetClassCoverage` | Gets overall coverage summary for a specific class |
| `GetMethodCoverage` | Gets overall coverage summary for a specific method |
| `GetProjectCoverage` | Gets overall coverage summary for a specific project/package |
| `GetSolutionCoverage` | Gets overall coverage summary for an entire solution |
| `GetSolutionMissedCoverage` | Gets a detailed report of ALL missed lines and branches across a solution |

## Usage

### Generating Coverage Reports

With MTP, code coverage is configured via `testconfig.json` and the `Microsoft.Testing.Extensions.CodeCoverage` extension. Add the extension to your test project and configure it for Cobertura output:

```json
{
    "extensions": [
        {
            "extensionId": "Microsoft.Testing.Extensions.CodeCoverage",
            "settings": {
                "format": "cobertura"
            }
        }
    ]
}
```

Then run your tests normally:

```bash
dotnet test
```

### Configuring as an MCP Server

The following examples are for end users consuming the published global tool. You do not need to build the project from source to use these configurations.

#### Claude Code CLI

Add the installed tool as a stdio MCP server with:

```bash
claude mcp add dotnet-mtp-coverage-mcp -- dotnet-mtp-coverage-mcp
```

If your local setup needs environment variables, Claude Code also supports:

```bash
claude mcp add -e SOME_SETTING=value dotnet-mtp-coverage-mcp -- dotnet-mtp-coverage-mcp
```

No additional subprocess flags or arguments are required when using the installed global tool.

#### JSON Configuration

For JSON-based MCP clients (for example, Claude Desktop or other clients that use an `mcpServers` object), use one of the following stdio server configurations.

Installed .NET tool:

```json
{
  "mcpServers": {
    "dotnet-mtp-coverage-mcp": {
      "command": "dotnet-mtp-coverage-mcp"
    }
  }
}
```

With environment variables:

```json
{
  "mcpServers": {
    "dotnet-mtp-coverage-mcp": {
      "command": "dotnet-mtp-coverage-mcp",
      "env": {
        "SOME_SETTING": "value"
      }
    }
  }
}
```

## Cobertura XML Format

This tool expects Cobertura XML format, which is the standard output from the `Microsoft.Testing.Extensions.CodeCoverage` extension when configured with `"format": "cobertura"` in `testconfig.json`.

## Building

```bash
cd src
dotnet build
```

## Running Tests

```bash
cd src
dotnet test
```

## Project Structure

```
src/
  UnitTestMcp.Core/         # Core library: models, parsers, services
    Models/                  # Coverage data models (records)
    Parsers/                 # Cobertura XML parser
    Services/                # Coverage query service
  UnitTestMcp.Server/        # MCP server executable
    Tools/                   # MCP tool definitions
  tests/
    UnitTestMcp.Tests/       # TUnit tests
      TestData/              # Sample Cobertura XML fixtures
```

## Attribution

The Cobertura XML parsing logic and coverage data model concepts in this project are adapted from [ReportGenerator](https://github.com/danielpalme/ReportGenerator) by Daniel Palme, licensed under the Apache License 2.0. ReportGenerator is a comprehensive code coverage report generation tool that supports many input and output formats.

## License

MIT License - Copyright (c) 2026 Glenn Watson. See [LICENSE](LICENSE) for details.
