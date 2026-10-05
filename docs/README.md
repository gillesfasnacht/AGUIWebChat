# AGUI WebChat

A Blazor Server (.NET 10) chat application featuring Fluent UI, an Ollama agent exposed through AG-UI, SignalR telemetry, and an AI model catalogue backed by SQL Server.

[Repository overview](../README.md) · [Architecture](ARCHITECTURE.md)

## Project Structure

- `src/Client/Components/Pages/Chat.razor`: chat page and response cancellation.
- `src/Client/Components/Chat/`: messages, settings, reasoning, and metrics.
- `src/Client/Components/Pages/Models.razor`: AI model catalogue listing.
- `src/Client/Services/`: AG-UI communication, settings, a SignalR connection per circuit, and the model catalogue API client.
- `src/Middleware/`: shared `AGUIWebChat.Middleware` library (logging interface, AG-UI event logger, UTF-8 SSE decoder, and read/write streams).
- `src/Client/Middleware/` and `src/Server/Middleware/`: application-specific HTTP integrations and adapters that preserve existing logging categories.
- `src/Server/Agents/ChatAgentFactory.cs`: agent creation and instrumentation.
- `src/Server/Inference/`: inference settings and validation.
- `src/Server/Telemetry/`: response monitoring and metrics publication.
- `src/Server/Hubs/`: SignalR connection handling.
- `src/Server/Endpoints/`: model catalogue HTTP endpoints.
- `src/Server/Services/AI/`: model catalogue operations and business validation.
- `src/Server/Domain/AI/`: provider, model, default settings, and reasoning effort entities.
- `src/Server/Data/` and `src/Server/Migrations/`: EF Core database context and SQL Server migrations.
- `src/Server/Mapping/`: mappings between entities and API models.
- `src/Contracts/`: DTOs shared by the server and client.
- `tests/AGUIWebChat.Client.Tests/`: model panel validation and the client SSE logger.
- `tests/AGUIWebChat.Contracts.Tests/`: DTO annotations, boundary values, and validation messages.
- `tests/AGUIWebChat.Middleware.Tests/`: shared SSE decoding, stream cancellation, read errors, and byte preservation.
- `tests/AGUIWebChat.Server.Tests/`: inference, telemetry, catalogue services, SQLite persistence, API endpoints, and the server SSE logger adapter.
- `tests/Directory.Build.props`: shared .NET and xUnit test configuration.
- `tests/Directory.Build.targets` and `tests/test.runsettings`: shared results directory configuration for MTP and VSTest.
- `docs/`: project documentation.

All paths and commands below are relative to the repository root, which contains `AGUIWebChat.slnx` and `global.json`.

## Getting Started

Install the .NET 10 SDK and ensure an Ollama server is accessible with your chosen model already installed.

The model catalogue also requires SQL Server with the application's migrations applied. The default connection string, `ConnectionStrings:ChatDatabase`, targets Windows SQL Server LocalDB. Configure it in the server's configuration files or override it with the `ConnectionStrings__ChatDatabase` environment variable for another SQL Server instance. SQLite is used only by the tests.

In the first PowerShell terminal:

```powershell
$env:OLLAMA_ENDPOINT="http://localhost:11434"
$env:OLLAMA_MODEL="granite4.2:8b"
dotnet run --project src/Server --launch-profile http
```

`OLLAMA_ENDPOINT` is required. `OLLAMA_MODEL` defaults to `granite4.2:8b`. Choose a model that supports the reasoning settings you use.

In a second terminal:

```powershell
$env:AGUI_SERVER_URL="http://localhost:5100"
dotnet run --project src/Client --launch-profile http
```

Open `http://localhost:5245`. The client's HTTPS profile uses `https://localhost:7219` and requires a trusted development certificate.

The server exposes `/ag-ui`, `/telemetry`, and `/api/models`. The client uses `AGUI_SERVER_URL` for chat, telemetry, and catalogue requests. Open `/models` in the client to view the catalogue. The **Stop** button cancels the current chat request; leaving the chat page also triggers cancellation.

## Swagger API Documentation

With the server's development profile running, open [Swagger UI](http://localhost:5100/swagger).
The OpenAPI document is available at http://localhost:5100/swagger/v1/swagger.json.

Swagger groups the catalogue endpoints under **AI Providers**, **AI Models**, and **AI Agents**.
Expand an operation, select **Try it out**, enter the parameters or JSON body, then select **Execute**.
Create a model before creating an agent and use the returned model identifier in the agent's request.
Write operations update the configured database.

The documentation describes request DTOs, response DTOs and the expected error responses.
Validation errors include the precise messages in `errors.model`.
Swagger is enabled in the Development and Testing environments; it is disabled in Production.
The SignalR telemetry connection is not an ordinary REST operation.

## Settings and Telemetry

Invalid chat inference settings fall back to their default values. Server-side limits are:

- Temperature: 0–2.
- Top-p: 0–1.
- Top-k: 1–1000.
- Context size: 1024–131072 tokens.
- Reasoning effort: `low`, `medium`, or `high`, selectable in Settings or with the button next to **Send**.

These application limits do not guarantee that the model or hardware can support the selected values.
Each Blazor circuit has its own connection and randomly generated telemetry channel, which is retained across SignalR reconnections. The server publishes only to that channel, without broadcasting to all clients. This mechanism separates circuits; it does not replace user authentication. Metrics are not replayed after a disconnection. A publication failure must not interrupt the model's response.

Each application stores its settings in `src/Client/appsettings*.json` or `src/Server/appsettings*.json`, with launch profiles in its `Properties/launchSettings.json` file. Keep secrets in environment variables or .NET user secrets. HTTP/SSE logs may contain conversations: configure logging levels and retention before shared use.

## Model Catalogue API

The model catalogue API (`/api/models`) returns errors as `application/problem+json`:

- `400`: invalid model data, unavailable provider, mismatched IDs, or malformed requests. Business validation responses include an `errors.model` array.
- `404`: the model requested by GET or PUT does not exist.
- `409`: the service detects a duplicate model for the provider.
- `500`: an unexpected error, with a neutral public message and a trace ID; internal details are logged on the server.

Deleting an absent model remains idempotent and returns `204`.

## Building and Testing

Run the following commands from the repository root:

```powershell
dotnet restore AGUIWebChat.slnx
dotnet build AGUIWebChat.slnx --configuration Release --no-restore
dotnet test --solution AGUIWebChat.slnx --configuration Release --no-build
```

The tests use xUnit v3 with Microsoft Testing Platform, configured in `global.json`. API tests supply their own Ollama configuration and an in-memory SQLite database. They require neither a running Ollama or SQL Server instance nor `OLLAMA_ENDPOINT` / `OLLAMA_MODEL` environment variables.

Run a single test project independently:

```powershell
dotnet test --project tests/AGUIWebChat.Client.Tests/AGUIWebChat.Client.Tests.csproj
dotnet test --project tests/AGUIWebChat.Contracts.Tests/AGUIWebChat.Contracts.Tests.csproj
dotnet test --project tests/AGUIWebChat.Middleware.Tests/AGUIWebChat.Middleware.Tests.csproj
dotnet test --project tests/AGUIWebChat.Server.Tests/AGUIWebChat.Server.Tests.csproj
```

The client tests do not reference the server. Contract and middleware tests reference only their corresponding libraries. SQLite and the ASP.NET Core test host packages are confined to the server test project. Shared SSE stream tests live in `AGUIWebChat.Middleware.Tests` and are not duplicated; application-specific logger adapters are tested in their respective client and server projects.

Test reports and attachments default to `tests/TestResults/`, which is excluded from Git. `tests/Directory.Build.props` defines the results path, and `tests/Directory.Build.targets` generates each test application's MTP configuration with `platformOptions.resultDirectory` during the build. The path is resolved from the repository's current location, so it works without a machine-specific path in source control. An explicit `--results-directory` option can override it.

For Visual Studio's VSTest mode with `xunit.runner.visualstudio`, the same shared props file selects `tests/test.runsettings`. If Visual Studio has a different solution-wide settings file selected, use **Test > Configure Run Settings > Select Solution Wide runsettings File** to select `tests/test.runsettings`. The MTP configuration is generated independently for Visual Studio's Microsoft Testing Platform mode. Rebuild the test projects after changing these settings. Existing results at the repository root are not moved automatically.

Coverage includes inference validation, fragmented SSE decoding, cancellation, read errors, telemetry, catalogue persistence, and API success and error responses. SQLite tests use `EnsureCreated`; they do not validate SQL Server migrations.

## Git

`.gitignore` excludes .NET build output, Visual Studio user files, and logs. `.gitattributes` normalizes text files to LF and Windows scripts to CRLF.

```powershell
git status
git add .
git diff --cached
git commit -m "Describe the change"
```
