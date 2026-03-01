using System.Text.Json.Serialization;

namespace CopilotInContainer.Infrastructure;

// ──────────────────────────────────────────────────────────────
// HTTP API request / response types
// ──────────────────────────────────────────────────────────────

public record HealthResponse(string Status, string Version);

public record ErrorResponse(string Error);

public record SimpleMessageResponse(string Message);

public record StartResponse(string Message, string ContainerId);

// GET /api/servers
public record ServerListResponse(List<ServerInstanceInfo> Instances);

public record ServerInstanceInfo(
    string InstanceName,
    string ContainerId,
    string ContainerName,
    int    Port,
    string? Model,
    string LogLevel,
    string StartedAt,
    string WorkspaceFolder,
    string Status,   // "running" | "stopped"
    string? Uptime);

// GET /api/servers/{name}/status
public record ServerStatusResponse(
    string  InstanceName,
    string  Status,
    string  ContainerId,
    string  ContainerName,
    int     Port,
    string? Model,
    string  LogLevel,
    string  StartedAt,
    string  WorkspaceFolder,
    string? Uptime);

// GET /api/servers/{name}/logs
public record ServerLogsResponse(string Logs);

// POST /api/servers/{name}/execute  – request body
public record ExecuteRequest(string Prompt);

// SSE payload variants for /execute
public record SseOutputLine(string Line, string Type);  // Type = "stdout" | "stderr"
public record SseDoneEvent(string EventType, int ExitCode); // EventType = "done"

// ──────────────────────────────────────────────────────────────
// AOT-safe JSON serialization context
// ──────────────────────────────────────────────────────────────

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(SimpleMessageResponse))]
[JsonSerializable(typeof(StartResponse))]
[JsonSerializable(typeof(ServerListResponse))]
[JsonSerializable(typeof(List<ServerInstanceInfo>))]
[JsonSerializable(typeof(ServerInstanceInfo))]
[JsonSerializable(typeof(ServerStatusResponse))]
[JsonSerializable(typeof(ServerLogsResponse))]
[JsonSerializable(typeof(ExecuteRequest))]
[JsonSerializable(typeof(SseOutputLine))]
[JsonSerializable(typeof(SseDoneEvent))]
internal partial class HttpServerJsonContext : JsonSerializerContext
{
}
