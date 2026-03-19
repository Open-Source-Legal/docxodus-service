# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
dotnet build                # Build the solution
dotnet run                  # Run the service (listens on port 8080)
dotnet test                 # Run all tests
dotnet test DocxodusService.Tests/ --filter "FullyQualifiedName~MethodName"  # Run a single test
```

## Architecture

This is a minimal ASP.NET Core (.NET 8) microservice that converts DOCX files to OpenContract JSON format. The entire API is defined in `Program.cs` using minimal API endpoints — there are no controllers.

**Core flow:** `POST /parse` receives a base64-encoded DOCX → decodes it → passes bytes to `Docxodus.WmlDocument` → calls `OpenContractExporter.Export()` → returns OpenContractDocExport as JSON.

The sole external library doing the heavy lifting is the [Docxodus](https://www.nuget.org/packages/Docxodus) NuGet package (`WmlDocument` and `OpenContractExporter` are its public API surface).

**JSON conventions:** The API accepts snake_case input (`docx_base64`, `filename` via `[JsonPropertyName]` in `Models/ParseRequest.cs`) but serializes output as camelCase (configured via `JsonNamingPolicy.CamelCase` in `Program.cs`). Null fields are omitted from responses.

**Tests:** xunit integration tests in `DocxodusService.Tests/` use `WebApplicationFactory<Program>` to spin up the app in-process (enabled by the `public partial class Program { }` declaration at the bottom of `Program.cs`). Tests create minimal DOCX files programmatically as ZIP archives — no fixture files on disk.

## Git Conventions

- Never include `Co-Authored-By` lines in commit messages.

## CI/CD

- **CI** (`.github/workflows/ci.yml`): Runs `dotnet restore` → `build` → `test` on PRs to main.
- **Publish** (`.github/workflows/publish.yml`): Builds and pushes Docker image to `ghcr.io` on push to main or version tags (`v*`).
