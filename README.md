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

First, run your tests with code coverage collection enabled:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Or if using MTP with `testconfig.json` configured for Cobertura output, simply:

```bash
dotnet test
```

### Configuring as an MCP Server

Add to your Claude Code MCP configuration (`~/.claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "unittest-mcp": {
      "command": "unittest-mcp",
      "args": []
    }
  }
}
```

## Cobertura XML Format

This tool expects Cobertura XML format, which is the standard output from:

- `dotnet test --collect:"XPlat Code Coverage"` (using coverlet)
- Microsoft.Testing.Extensions.CodeCoverage with `"format": "cobertura"` in `testconfig.json`

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
