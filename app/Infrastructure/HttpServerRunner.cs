using System.Diagnostics;
using System.Text.Json;
using CopilotInContainer.Commands;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Embedded ASP.NET Core Minimal API server that exposes cic commands over HTTP.
/// Launched via `cic http-server start --port 5000 [--api-key &lt;token&gt;]`.
/// </summary>
public static class HttpServerRunner
{
    public static async Task RunAsync(int port, string? apiKey)
    {
        var builder = WebApplication.CreateSlimBuilder();

        // Register AOT-safe JSON serializer context so all route handlers use it.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, HttpServerJsonContext.Default);
        });

        var app = builder.Build();

        // Bind to the requested port
        app.Urls.Add($"http://0.0.0.0:{port}");

        // ── Optional API-key middleware ────────────────────────────────────────
        if (!string.IsNullOrEmpty(apiKey))
        {
            app.Use(async (ctx, next) =>
            {
                // Health endpoint is always public.
                if (ctx.Request.Path.StartsWithSegments("/health"))
                {
                    await next(ctx);
                    return;
                }

                var header = ctx.Request.Headers.Authorization.FirstOrDefault();
                if (header != $"Bearer {apiKey}")
                {
                    ctx.Response.StatusCode  = 401;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(
                        JsonSerializer.Serialize(
                            new ErrorResponse("Unauthorized — provide 'Authorization: Bearer <api-key>'"),
                            HttpServerJsonContext.Default.ErrorResponse));
                    return;
                }

                await next(ctx);
            });
        }

        // ── Routes ─────────────────────────────────────────────────────────────

        // GET /health
        app.MapGet("/health", () =>
            Results.Json(new HealthResponse("ok", "1.0.0"),
                HttpServerJsonContext.Default.HealthResponse));

        // GET /api/servers
        app.MapGet("/api/servers", async () =>
        {
            var instances = await GetAllInstancesAsync();
            return Results.Json(new ServerListResponse(instances),
                HttpServerJsonContext.Default.ServerListResponse);
        });

        // GET /api/servers/{name}/status
        app.MapGet("/api/servers/{name}/status", async (string name) =>
        {
            var state = await LoadStateAsync(name);
            if (state is null)
                return Results.Json(new ErrorResponse($"Server instance '{name}' not found"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 404);

            var running = ContainerRunner.GetRuntime().IsContainerRunning(state.ContainerId);
            return Results.Json(
                new ServerStatusResponse(
                    InstanceName:    state.InstanceName,
                    Status:          running ? "running" : "stopped",
                    ContainerId:     state.ContainerId,
                    ContainerName:   state.ContainerName,
                    Port:            state.Port,
                    Model:           state.Model,
                    LogLevel:        state.LogLevel,
                    StartedAt:       state.StartedAt.ToString("o"),
                    WorkspaceFolder: state.WorkspaceFolder,
                    Uptime:          running ? Uptime(state.StartedAt) : null),
                HttpServerJsonContext.Default.ServerStatusResponse);
        });

        // GET /api/servers/{name}/logs?tail=N
        app.MapGet("/api/servers/{name}/logs", async (string name, int? tail) =>
        {
            var state = await LoadStateAsync(name);
            if (state is null)
                return Results.Json(new ErrorResponse($"Server instance '{name}' not found"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 404);

            var runtime = ContainerRunner.GetRuntime();
            var logArgs = runtime.BuildLogsArguments(state.ContainerId, tail, follow: false);
            var (exitCode, logs) = await ContainerRunner.RunCommandAsync(runtime.CommandName, logArgs.ToArray());

            if (exitCode != 0)
                return Results.Json(new ErrorResponse($"Failed to get logs: {logs}"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 500);

            return Results.Json(new ServerLogsResponse(logs),
                HttpServerJsonContext.Default.ServerLogsResponse);
        });

        // POST /api/servers/{name}/stop
        app.MapPost("/api/servers/{name}/stop", async (string name) =>
        {
            var state = await LoadStateAsync(name);
            if (state is null)
                return Results.Json(new ErrorResponse($"Server instance '{name}' not found"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 404);

            var runtime = ContainerRunner.GetRuntime();
            var (exitCode, output) = runtime.StopContainer(state.ContainerId);

            if (exitCode != 0)
                return Results.Json(new ErrorResponse($"Failed to stop container: {output}"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 500);

            var stateFile = StateFilePath(name);
            if (File.Exists(stateFile)) File.Delete(stateFile);

            return Results.Json(new SimpleMessageResponse($"Server '{name}' stopped"),
                HttpServerJsonContext.Default.SimpleMessageResponse);
        });

        // POST /api/servers/{name}/start
        // Re-starts an existing container whose state file was created via `cic server start`.
        app.MapPost("/api/servers/{name}/start", async (string name) =>
        {
            var state = await LoadStateAsync(name);
            if (state is null)
                return Results.Json(
                    new ErrorResponse($"No state file for '{name}'. Create the server locally first: cic server start --name {name}"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 404);

            var runtime = ContainerRunner.GetRuntime();
            if (runtime.IsContainerRunning(state.ContainerId))
                return Results.Json(new ErrorResponse($"Server '{name}' is already running"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 409);

            var (exitCode, output) = await ContainerRunner.RunCommandAsync(
                runtime.CommandName, "start", state.ContainerId);

            if (exitCode != 0)
                return Results.Json(new ErrorResponse($"Failed to start container: {output}"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 500);

            return Results.Json(new StartResponse($"Server '{name}' started", state.ContainerId),
                HttpServerJsonContext.Default.StartResponse);
        });

        // POST /api/servers/{name}/restart
        app.MapPost("/api/servers/{name}/restart", async (string name) =>
        {
            var state = await LoadStateAsync(name);
            if (state is null)
                return Results.Json(new ErrorResponse($"No state file for '{name}'"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 404);

            var runtime = ContainerRunner.GetRuntime();

            if (runtime.IsContainerRunning(state.ContainerId))
            {
                var (stopCode, stopOut) = runtime.StopContainer(state.ContainerId);
                if (stopCode != 0)
                    return Results.Json(new ErrorResponse($"Failed to stop container: {stopOut}"),
                        HttpServerJsonContext.Default.ErrorResponse, statusCode: 500);
            }

            var (startCode, startOut) = await ContainerRunner.RunCommandAsync(
                runtime.CommandName, "start", state.ContainerId);

            if (startCode != 0)
                return Results.Json(new ErrorResponse($"Failed to restart container: {startOut}"),
                    HttpServerJsonContext.Default.ErrorResponse, statusCode: 500);

            return Results.Json(new SimpleMessageResponse($"Server '{name}' restarted"),
                HttpServerJsonContext.Default.SimpleMessageResponse);
        });

        // POST /api/servers/{name}/execute  →  SSE streaming
        app.MapPost("/api/servers/{name}/execute", async (string name, HttpContext ctx) =>
        {
            // Parse request body
            ExecuteRequest? req;
            try
            {
                req = await ctx.Request.ReadFromJsonAsync(
                    HttpServerJsonContext.Default.ExecuteRequest,
                    ctx.RequestAborted);
            }
            catch
            {
                ctx.Response.StatusCode  = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    JsonSerializer.Serialize(new ErrorResponse("Invalid JSON body"),
                        HttpServerJsonContext.Default.ErrorResponse));
                return;
            }

            if (req is null || string.IsNullOrWhiteSpace(req.Prompt))
            {
                ctx.Response.StatusCode  = 400;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    JsonSerializer.Serialize(new ErrorResponse("Missing required field 'prompt'"),
                        HttpServerJsonContext.Default.ErrorResponse));
                return;
            }

            var state = await LoadStateAsync(name);
            if (state is null)
            {
                ctx.Response.StatusCode  = 404;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    JsonSerializer.Serialize(new ErrorResponse($"Server instance '{name}' not found"),
                        HttpServerJsonContext.Default.ErrorResponse));
                return;
            }

            var runtime = ContainerRunner.GetRuntime();
            if (!runtime.IsContainerRunning(state.ContainerId))
            {
                ctx.Response.StatusCode  = 409;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(
                    JsonSerializer.Serialize(new ErrorResponse($"Server '{name}' is not running"),
                        HttpServerJsonContext.Default.ErrorResponse));
                return;
            }

            // Set SSE response headers before writing anything
            ctx.Response.ContentType = "text/event-stream; charset=utf-8";
            ctx.Response.Headers.CacheControl  = "no-cache";
            ctx.Response.Headers.Connection    = "keep-alive";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

            // Build `container exec` arguments (non-interactive, with prompt flag)
            var copilotArgs = new[] { "copilot", "-p", req.Prompt };
            var execArgs    = runtime.BuildExecArguments(
                state.ContainerId, "/workspace", interactive: false, copilotArgs);

            var psi = new ProcessStartInfo
            {
                FileName               = runtime.CommandName,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            };
            foreach (var a in execArgs) psi.ArgumentList.Add(a);

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // Helpers that write one SSE "data:" frame
            async Task WriteSse<T>(T payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
            {
                var json    = JsonSerializer.Serialize(payload, typeInfo);
                var frame   = $"data: {json}\n\n";
                await ctx.Response.WriteAsync(frame, ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
            }

            process.OutputDataReceived += async (_, e) =>
            {
                if (e.Data is null) return;
                try { await WriteSse(new SseOutputLine(e.Data, "stdout"), HttpServerJsonContext.Default.SseOutputLine); }
                catch (OperationCanceledException) { }
            };

            process.ErrorDataReceived += async (_, e) =>
            {
                if (e.Data is null) return;
                try { await WriteSse(new SseOutputLine(e.Data, "stderr"), HttpServerJsonContext.Default.SseOutputLine); }
                catch (OperationCanceledException) { }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ctx.RequestAborted);

            // Final "done" event so the client knows the command finished
            try
            {
                await WriteSse(new SseDoneEvent("done", process.ExitCode),
                    HttpServerJsonContext.Default.SseDoneEvent);
            }
            catch (OperationCanceledException) { }
        });

        // ── Start ──────────────────────────────────────────────────────────────
        PrintStartupBanner(port, apiKey);
        await app.RunAsync();
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string StateDir() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot-in-container", "servers");

    private static string StateFilePath(string name) =>
        Path.Combine(StateDir(), $"{name}.json");

    private static async Task<ServerState?> LoadStateAsync(string name)
    {
        var file = StateFilePath(name);
        if (!File.Exists(file)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(file);
            return JsonSerializer.Deserialize(json, ServerStateJsonContext.Default.ServerState);
        }
        catch { return null; }
    }

    private static async Task<List<ServerInstanceInfo>> GetAllInstancesAsync()
    {
        var dir       = StateDir();
        var instances = new List<ServerInstanceInfo>();
        if (!Directory.Exists(dir)) return instances;

        var runtime = ContainerRunner.GetRuntime();

        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            try
            {
                var json  = await File.ReadAllTextAsync(file);
                var state = JsonSerializer.Deserialize(json, ServerStateJsonContext.Default.ServerState);
                if (state is null) continue;

                var running = runtime.IsContainerRunning(state.ContainerId);
                instances.Add(new ServerInstanceInfo(
                    InstanceName:    state.InstanceName,
                    ContainerId:     state.ContainerId,
                    ContainerName:   state.ContainerName,
                    Port:            state.Port,
                    Model:           state.Model,
                    LogLevel:        state.LogLevel,
                    StartedAt:       state.StartedAt.ToString("o"),
                    WorkspaceFolder: state.WorkspaceFolder,
                    Status:          running ? "running" : "stopped",
                    Uptime:          running ? Uptime(state.StartedAt) : null));
            }
            catch { /* skip corrupt state files */ }
        }

        return instances;
    }

    private static string Uptime(DateTime startedAt)
    {
        var u = DateTime.UtcNow - startedAt;
        if (u.TotalDays    >= 1) return $"{(int)u.TotalDays}d {u.Hours}h";
        if (u.TotalHours   >= 1) return $"{(int)u.TotalHours}h {u.Minutes}m";
        if (u.TotalMinutes >= 1) return $"{(int)u.TotalMinutes}m {u.Seconds}s";
        return $"{(int)u.TotalSeconds}s";
    }

    private static void PrintStartupBanner(int port, string? apiKey)
    {
        ConsoleUI.PrintSuccess($"HTTP server listening on http://0.0.0.0:{port}");
        if (!string.IsNullOrEmpty(apiKey))
            ConsoleUI.PrintInfo("API key authentication enabled (Authorization: Bearer <key>)");
        Console.WriteLine();
        Console.WriteLine("Endpoints:");
        Console.WriteLine("  GET  /health");
        Console.WriteLine("  GET  /api/servers");
        Console.WriteLine("  GET  /api/servers/{name}/status");
        Console.WriteLine("  GET  /api/servers/{name}/logs?tail=N");
        Console.WriteLine("  POST /api/servers/{name}/start");
        Console.WriteLine("  POST /api/servers/{name}/stop");
        Console.WriteLine("  POST /api/servers/{name}/restart");
        Console.WriteLine("  POST /api/servers/{name}/execute   (SSE streaming)");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop");
        Console.WriteLine();
    }
}
