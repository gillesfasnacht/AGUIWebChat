# AGUI WebChat

A Blazor Server (.NET 10) chat application featuring Fluent UI, an Ollama agent exposed through AG-UI, and SignalR telemetry.

## Project Structure

- `Client/Components/Pages/Chat.razor`: chat page and response cancellation.
- `Client/Components/Chat/`: messages, settings, reasoning, and metrics.
- `Client/Services/`: AG-UI communication, settings, and a SignalR connection per circuit.
- `Middleware/`: shared `AGUIWebChat.Middleware` library (logging interface, AG-UI event logger, UTF-8 SSE decoder, and read/write streams).
- `Client/Middleware/` and `Server/Middleware/`: application-specific HTTP integrations and adapters that preserve existing logging categories.
- `Server/Agents/ChatAgentFactory.cs`: agent creation and instrumentation.
- `Server/Inference/`: inference settings and validation.
- `Server/Telemetry/`: response monitoring and metrics publication.
- `Server/Hubs/`: SignalR connection handling.
- `Contracts/`: DTOs shared by the server and client.
- `tests/AGUIWebChat.Tests/`: automated tests.

## Getting Started

Install the .NET 10 SDK and ensure an Ollama server is accessible with your chosen model already installed.

In the first PowerShell terminal:

```powershell
$env:OLLAMA_ENDPOINT="http://localhost:11434"
$env:OLLAMA_MODEL="granite4.2:8b"
dotnet run --project Server --launch-profile http
```

`OLLAMA_ENDPOINT` is required. `OLLAMA_MODEL` defaults to `granite4.2:8b`. Choose a model that supports the reasoning settings you use.

In a second terminal:

```powershell
$env:AGUI_SERVER_URL="http://localhost:5100"
dotnet run --project Client --launch-profile http
```

Open `http://localhost:5245`. The client's HTTPS profile uses `https://localhost:7219` and requires a trusted development certificate.

The server exposes `/ag-ui` and `/telemetry`. The client uses `AGUI_SERVER_URL` for both connections. The **Stop** button cancels the current request; leaving the page also triggers cancellation.

## Settings and Telemetry

Invalid settings fall back to their default values. Server-side limits are: temperature 0–2, top-p 0–1, top-k 1–1000, context size 1024–131072, and reasoning effort `low`, `medium`, or `high`. These application limits do not guarantee that the model or hardware can support the selected values.

Each Blazor circuit has its own connection and randomly generated telemetry channel, which is retained across SignalR reconnections. The server publishes only to that channel, without broadcasting to all clients. This mechanism separates circuits; it does not replace user authentication. Metrics are not replayed after a disconnection. A publication failure must not interrupt the model's response.

Common settings are stored in `appsettings*.json`, and local launch profiles are in `Properties/launchSettings.json`. Keep secrets in environment variables or .NET user secrets. HTTP/SSE logs may contain conversations: configure logging levels and retention before shared use.

## Building and Testing

The model catalogue API (`/api/models`) returns errors as `application/problem+json`:

- `400`: invalid model data, unavailable provider, mismatched IDs, or malformed requests. Business validation responses include an `errors.model` array.
- `404`: the model requested by GET or PUT does not exist.
- `409`: the service detects a duplicate model for the provider.
- `500`: an unexpected error, with a neutral public message and a trace ID; internal details are logged on the server.

Deleting an absent model remains idempotent and returns `204`.
API tests supply their own Ollama configuration and an in-memory SQLite database. They do not use a local Ollama instance or require `OLLAMA_ENDPOINT` / `OLLAMA_MODEL` environment variables.

```powershell
dotnet restore AGUIWebChat.slnx
dotnet build AGUIWebChat.slnx --configuration Release --no-restore
dotnet test --solution AGUIWebChat.slnx --configuration Release --no-build
```

The tests use xUnit v3 with Microsoft Testing Platform, configured in `global.json`. They do not require Ollama. They cover invalid settings, fragmented SSE decoding, cancellation, and read errors, among other cases.

## Git

`.gitignore` excludes .NET build output, Visual Studio user files, and logs. `.gitattributes` normalizes text files to LF and Windows scripts to CRLF.

```powershell
git status
git add .
git diff --cached
git commit -m "Describe the change"
```
