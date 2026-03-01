using System.CommandLine;
using CopilotInContainer.Infrastructure;

namespace CopilotInContainer.Commands;

/// <summary>
/// Registers the `cic http-server` sub-command family.
///
/// Usage:
///   cic http-server start [--port 5000] [--api-key &lt;token&gt;]
/// </summary>
public class HttpServerCommands : ICommand
{
    public void Configure(RootCommand rootCommand)
    {
        var httpServerCommand = new Command(
            "http-server",
            "Run cic as an HTTP API server so you can manage Copilot server instances over HTTP");

        // ── start ──────────────────────────────────────────────
        var startCommand = new Command(
            "start",
            "Start the cic HTTP API server");

        var portOption = new Option<int>(
            "--port",
            description: "TCP port to listen on",
            getDefaultValue: () => 5000);

        var apiKeyOption = new Option<string?>(
            "--api-key",
            description: "Optional static bearer token. When set every request (except GET /health) " +
                         "must include 'Authorization: Bearer <api-key>'");

        startCommand.AddOption(portOption);
        startCommand.AddOption(apiKeyOption);

        startCommand.SetHandler(async (int port, string? apiKey) =>
        {
            await HttpServerRunner.RunAsync(port, apiKey);
        }, portOption, apiKeyOption);

        httpServerCommand.AddCommand(startCommand);
        rootCommand.AddCommand(httpServerCommand);
    }
}
