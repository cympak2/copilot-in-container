using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CopilotInContainer.Infrastructure;

namespace CopilotInContainer.Commands;

/// <summary>
/// Commands for managing GitHub Copilot CLI server instances.
/// </summary>
public partial class ServerCommands : ICommand
{
    private const string ServerStateDir = ".copilot-in-container/servers";
    
    private static string GetServerStateFile(string instanceName)
    {
        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ServerStateDir
        );
        Directory.CreateDirectory(stateDir);
        return Path.Combine(stateDir, $"{instanceName}.json");
    }

    public void Configure(RootCommand rootCommand)
    {
        var serverCommand = new Command("server", "Manage GitHub Copilot CLI server instances");

        // Start server command
        var startCommand = new Command("start", "Start a named Copilot server instance");
        var nameOption = new Option<string>(
            "--name",
            description: "Instance name (default: 'default')",
            getDefaultValue: () => "default"
        );
        var portOption = new Option<int?>(
            "--port",
            description: "TCP port for the server (auto-assigned if not specified)"
        );
        var modelOption = new Option<string?>(
            "--model",
            description: "Default AI model for this server instance"
        );
        var logLevelOption = new Option<string>(
            "--log-level",
            description: "Log level for the server",
            getDefaultValue: () => "info"
        );
        var mcpConfigOption = new Option<string?>(
            "--mcp-config",
            description: "Path to MCP config directory (overrides default and global config)"
        );
        var noMcpInstallOption = new Option<bool>(
            "--no-mcp-install",
            description: "Skip automatic MCP server dependency installation",
            getDefaultValue: () => false
        );
        
        startCommand.AddOption(nameOption);
        startCommand.AddOption(portOption);
        startCommand.AddOption(modelOption);
        startCommand.AddOption(logLevelOption);
        startCommand.AddOption(mcpConfigOption);
        startCommand.AddOption(noMcpInstallOption);
        
        startCommand.SetHandler(async (string name, int? port, string? model, string logLevel, string? mcpConfig, bool noMcpInstall) =>
        {
            await StartServerAsync(name, port, model, logLevel, mcpConfig, !noMcpInstall);
        }, nameOption, portOption, modelOption, logLevelOption, mcpConfigOption, noMcpInstallOption);

        // Stop server command
        var stopCommand = new Command("stop", "Stop a running Copilot server instance");
        var stopNameOption = new Option<string>(
            "--name",
            description: "Instance name (default: 'default')",
            getDefaultValue: () => "default"
        );
        stopCommand.AddOption(stopNameOption);
        stopCommand.SetHandler(async (string name) =>
        {
            await StopServerAsync(name);
        }, stopNameOption);

        // List servers command
        var listCommand = new Command("list", "List all Copilot server instances");
        listCommand.SetHandler(async () =>
        {
            await ListServersAsync();
        });

        // Connect to server command
        var connectCommand = new Command("connect", "Connect to a running Copilot server instance");
        var connectNameOption = new Option<string>(
            "--name",
            description: "Instance name (default: 'default')",
            getDefaultValue: () => "default"
        );
        var noTtyOption = new Option<bool>(
            "--no-tty",
            description: "Run without interactive terminal (for non-interactive use)",
            getDefaultValue: () => false
        );
        var promptArgument = new Argument<string[]>(
            "prompt",
            "Prompt to send to the server"
        ) { Arity = ArgumentArity.ZeroOrMore };
        
        connectCommand.AddOption(connectNameOption);
        connectCommand.AddOption(noTtyOption);
        connectCommand.AddArgument(promptArgument);
        connectCommand.SetHandler(async (string name, bool noTty, string[] prompt) =>
        {
            await ConnectToServerAsync(name, noTty, prompt);
        }, connectNameOption, noTtyOption, promptArgument);

        // Status command
        var statusCommand = new Command("status", "Show status of a Copilot server instance");
        var statusNameOption = new Option<string>(
            "--name",
            description: "Instance name (default: 'default')",
            getDefaultValue: () => "default"
        );
        statusCommand.AddOption(statusNameOption);
        statusCommand.SetHandler(async (string name) =>
        {
            await ShowServerStatusAsync(name);
        }, statusNameOption);

        // Logs command
        var logsCommand = new Command("logs", "Show logs from a Copilot server instance");
        var logsNameOption = new Option<string>(
            "--name",
            description: "Instance name (default: 'default')",
            getDefaultValue: () => "default"
        );
        var tailOption = new Option<int?>(
            "--tail",
            description: "Number of lines to show from the end of the logs"
        );
        var followOption = new Option<bool>(
            "--follow",
            description: "Follow log output",
            getDefaultValue: () => false
        );
        logsCommand.AddOption(logsNameOption);
        logsCommand.AddOption(tailOption);
        logsCommand.AddOption(followOption);
        logsCommand.SetHandler(async (string name, int? tail, bool follow) =>
        {
            await ShowServerLogsAsync(name, tail, follow);
        }, logsNameOption, tailOption, followOption);

        serverCommand.AddCommand(startCommand);
        serverCommand.AddCommand(stopCommand);
        serverCommand.AddCommand(listCommand);
        serverCommand.AddCommand(connectCommand);
        serverCommand.AddCommand(statusCommand);
        serverCommand.AddCommand(logsCommand);
        
        rootCommand.AddCommand(serverCommand);
    }

    private async Task StartServerAsync(string instanceName, int? port, string? model, string logLevel, string? mcpConfigOverride = null, bool installMcpDeps = true)
    {
        var stateFile = GetServerStateFile(instanceName);
        
        // Check if instance already exists
        if (File.Exists(stateFile))
        {
            var existingState = await LoadServerStateAsync(instanceName);
            if (existingState != null && IsContainerRunning(existingState.ContainerId))
            {
                ConsoleUI.PrintWarning($"Server instance '{instanceName}' is already running");
                Console.WriteLine($"  Port: {existingState.Port}");
                Console.WriteLine($"  Container: {existingState.ContainerId}");
                Console.WriteLine();
                Console.WriteLine("Use 'cic server stop --name {instanceName}' to stop it first");
                return;
            }
        }

        // Get current directory to mount as workspace
        var currentDir = Directory.GetCurrentDirectory();
        
        ConsoleUI.PrintInfo($"Starting Copilot server instance: {instanceName}");
        Console.WriteLine($"  Workspace folder: {currentDir}");
        Console.WriteLine();

        // Ensure config directory exists
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "gh-copilot"
        );
        Directory.CreateDirectory(configDir);

        // Get GitHub token
        var (tokenExitCode, githubToken) = await ContainerRunner.RunCommandAsync("gh", "auth", "token");
        if (tokenExitCode != 0 || string.IsNullOrWhiteSpace(githubToken))
        {
            ConsoleUI.PrintError("Failed to retrieve GitHub token");
            return;
        }

        // Get runtime
        var runtime = ContainerRunner.GetRuntime();
        
        // Get user and group IDs
        var userId = ContainerRunner.GetUserId();
        var groupId = ContainerRunner.GetGroupId();

        // Generate container name
        var containerName = $"copilot-server-{instanceName}";
        
        // Build environment variables
        var envVars = new Dictionary<string, string>
        {
            { "PUID", userId },
            { "PGID", groupId },
            { "GITHUB_TOKEN", githubToken }
        };
        
        // Build volumes
        var volumes = new Dictionary<string, string>
        {
            { currentDir, "/workspace:rw" },
            { configDir, "/home/appuser/.copilot:rw" }
        };

        // Handle MCP configuration
        var mcpConfigPath = McpCommands.GetEffectiveMcpConfigPath(mcpConfigOverride);
        string? mcpContainerPath = null;

        if (mcpConfigPath != null)
        {
            var mcpConfigDir = Path.GetDirectoryName(mcpConfigPath)!;
            
            // Mount MCP config directory to container
            mcpContainerPath = "/mcp/readonly";
            volumes.Add(mcpConfigDir, $"{mcpContainerPath}:ro");
            
            ConsoleUI.PrintInfo($"MCP config: {mcpConfigPath}");
            
            // Handle MCP server dependency installation
            if (installMcpDeps)
            {
                await ContainerRunner.InstallMcpDependencies(runtime, mcpConfigPath, volumes, envVars, containerName);
            }
        }
        
        // Build ports if specified
        Dictionary<string, string>? ports = null;
        if (port.HasValue)
        {
            ports = new Dictionary<string, string>
            {
                { port.Value.ToString(), port.Value.ToString() }
            };
        }

        // Build container arguments for server mode
        var args = runtime.BuildRunArguments(
            "copilot-in-container:latest",
            containerName,
            envVars,
            volumes,
            "/workspace",
            interactive: false,
            removeOnExit: false,
            ports: ports,
            detached: true
        );
        
        // Add copilot command and arguments
        args.Add("copilot");
        args.Add("--server");
        args.Add("--log-level");
        args.Add(logLevel);
        
        // Add port if specified
        if (port.HasValue)
        {
            args.Add("--port");
            args.Add(port.Value.ToString());
        }

        // Add MCP config if available
        if (mcpContainerPath != null)
        {
            args.Add("--additional-mcp-config");
            args.Add($"{mcpContainerPath}/mcp-config.json");
        }

        // Start container
        var (exitCode, output) = await ContainerRunner.RunCommandAsync(runtime.CommandName, args.ToArray());
        
        if (exitCode != 0)
        {
            ConsoleUI.PrintError("Failed to start Copilot server");
            if (!string.IsNullOrEmpty(output))
            {
                Console.WriteLine("Container run error:");
                Console.WriteLine(output);
            }
            else
            {
                Console.WriteLine("No error output received. Command was:");
                Console.WriteLine("container " + string.Join(" ", args));
            }
            return;
        }

        var containerId = output.Trim();

        // Wait for server to announce port
        int? detectedPort = port;
        if (!port.HasValue)
        {
            ConsoleUI.PrintInfo("Waiting for server to start...");
            detectedPort = await WaitForServerPortAsync(containerId);
            
            if (!detectedPort.HasValue)
            {
                ConsoleUI.PrintError("Failed to detect server port");
                await ContainerRunner.RunCommandAsync(runtime.CommandName, "stop", containerId);
                return;
            }
            
            // Now we know the port - we need to stop the container and restart it with the port published
            ConsoleUI.PrintInfo($"Server started on internal port {detectedPort}, reconfiguring with published port...");
            await ContainerRunner.RunCommandAsync(runtime.CommandName, "stop", containerId);
            await ContainerRunner.RunCommandAsync(runtime.CommandName, "rm", containerId);
            
            // Rebuild arguments with the now-known port published
            ports = new Dictionary<string, string>
            {
                { detectedPort.Value.ToString(), detectedPort.Value.ToString() }
            };
            
            args = runtime.BuildRunArguments(
                "copilot-in-container:latest",
                containerName,
                envVars,
                volumes,
                "/workspace",
                interactive: false,
                removeOnExit: false,
                ports: ports,
                detached: true
            );
            
            args.Add("copilot");
            args.Add("--server");
            args.Add("--log-level");
            args.Add(logLevel);
            args.Add("--port");
            args.Add(detectedPort.Value.ToString());
            
            // Add MCP config if available
            if (mcpContainerPath != null)
            {
                args.Add("--additional-mcp-config");
                args.Add($"{mcpContainerPath}/mcp-config.json");
            }
            
            var (restartExitCode, restartOutput) = await ContainerRunner.RunCommandAsync(runtime.CommandName, args.ToArray());
            if (restartExitCode != 0)
            {
                ConsoleUI.PrintError("Failed to restart Copilot server with published port");
                if (!string.IsNullOrEmpty(restartOutput))
                {
                    Console.WriteLine("Container run error:");
                    Console.WriteLine(restartOutput);
                }
                return;
            }
            
            containerId = restartOutput.Trim();
        }

        // Save server state
        var state = new ServerState
        {
            InstanceName = instanceName,
            ContainerId = containerId,
            ContainerName = containerName,
            Port = detectedPort ?? 0,
            Model = model,
            LogLevel = logLevel,
            StartedAt = DateTime.UtcNow,
            WorkspaceFolder = currentDir,
            McpConfigPath = mcpConfigPath
        };

        await SaveServerStateAsync(state);

        ConsoleUI.PrintSuccess($"Server instance '{instanceName}' started successfully");
        Console.WriteLine();
        Console.WriteLine($"  Container ID: {containerId[..12]}");
        Console.WriteLine($"  Port: {detectedPort}");
        if (model != null)
            Console.WriteLine($"  Model: {model}");
        Console.WriteLine();
        Console.WriteLine("Connect to this instance:");
        Console.WriteLine($"  cic server connect --name {instanceName}");
        Console.WriteLine();
    }

    private async Task<int?> WaitForServerPortAsync(string containerId)
    {
        var timeout = TimeSpan.FromSeconds(30);
        var start = DateTime.UtcNow;

        ConsoleUI.PrintInfo("Detecting server port from logs...");

        while (DateTime.UtcNow - start < timeout)
        {
            // Get runtime
            var runtime = ContainerRunner.GetRuntime();
            
            // Check if container is still running
            var (statusCode, statusOutput) = runtime.ListContainers();
            
            bool isRunning = false;
            if (statusCode == 0)
            {
                var lines = statusOutput.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains(containerId) && line.Contains("running", StringComparison.OrdinalIgnoreCase))
                    {
                        isRunning = true;
                        break;
                    }
                }
            }
            
            if (!isRunning)
            {
                ConsoleUI.PrintError("Container stopped unexpectedly");
                // Get last logs to show error
                var logsArgs = runtime.BuildLogsArguments(containerId);
                var (_, errorLogs) = await ContainerRunner.RunCommandAsync(runtime.CommandName, logsArgs.ToArray());
                Console.WriteLine("Last logs:");
                Console.WriteLine(errorLogs);
                return null;
            }

            var logArgs = runtime.BuildLogsArguments(containerId);
            var (exitCode, logs) = await ContainerRunner.RunCommandAsync(runtime.CommandName, logArgs.ToArray());
            
            if (exitCode == 0 && !string.IsNullOrEmpty(logs))
            {
                // Look for "listening on port XXXX" in logs
                var lines = logs.Split('\n');
                foreach (var line in lines)
                {
                    // Try multiple patterns
                    var patterns = new[]
                    {
                        @"listening on port (\d+)",
                        @"Server listening on .*:(\d+)",
                        @"port\s+(\d+)"
                    };

                    foreach (var pattern in patterns)
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(
                            line, 
                            pattern, 
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase
                        );
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
                        {
                            ConsoleUI.PrintSuccess($"Detected port: {port}");
                            return port;
                        }
                    }
                }
            }

            await Task.Delay(500);
        }

        ConsoleUI.PrintError("Timeout waiting for port announcement");
        return null;
    }

    private async Task StopServerAsync(string instanceName)
    {
        var state = await LoadServerStateAsync(instanceName);
        
        if (state == null)
        {
            ConsoleUI.PrintError($"Server instance '{instanceName}' not found");
            Console.WriteLine();
            Console.WriteLine("Use 'cic server list' to see available instances");
            return;
        }

        ConsoleUI.PrintInfo($"Stopping server instance: {instanceName}");
        
        var runtime = ContainerRunner.GetRuntime();
        var (exitCode, output) = runtime.StopContainer(state.ContainerId);
        
        if (exitCode != 0)
        {
            ConsoleUI.PrintError("Failed to stop server");
            Console.WriteLine(output);
            return;
        }

        // Remove state file
        var stateFile = GetServerStateFile(instanceName);
        if (File.Exists(stateFile))
        {
            File.Delete(stateFile);
        }

        ConsoleUI.PrintSuccess($"Server instance '{instanceName}' stopped");
        Console.WriteLine();
    }

    private async Task ListServersAsync()
    {
        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ServerStateDir
        );

        if (!Directory.Exists(stateDir))
        {
            Console.WriteLine("No server instances found");
            Console.WriteLine();
            Console.WriteLine("Start a new instance:");
            Console.WriteLine("  cic server start --name myserver");
            return;
        }

        var stateFiles = Directory.GetFiles(stateDir, "*.json");
        
        if (stateFiles.Length == 0)
        {
            Console.WriteLine("No server instances found");
            Console.WriteLine();
            Console.WriteLine("Start a new instance:");
            Console.WriteLine("  cic server start --name myserver");
            return;
        }

        Console.WriteLine("Copilot Server Instances:");
        Console.WriteLine();
        Console.WriteLine("NAME              STATUS     PORT    UPTIME          WORKSPACE");
        Console.WriteLine("────────────────  ─────────  ──────  ──────────────  ────────────────────────────────────────");

        foreach (var stateFile in stateFiles)
        {
            var instanceName = Path.GetFileNameWithoutExtension(stateFile);
            var state = await LoadServerStateAsync(instanceName);
            
            if (state == null) continue;

            var isRunning = IsContainerRunning(state.ContainerId);
            var status = isRunning ? "Running" : "Stopped";
            var uptime = isRunning ? GetUptime(state.StartedAt) : "-";
            var workspace = state.WorkspaceFolder ?? "-";
            
            // Shorten workspace path if it's in home directory
            if (workspace.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
            {
                workspace = "~" + workspace.Substring(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Length);
            }

            Console.WriteLine(
                $"{instanceName,-16}  {status,-9}  {state.Port,-6}  {uptime,-14}  {workspace}"
            );
        }

        Console.WriteLine();
    }

    private async Task ConnectToServerAsync(string instanceName, bool noTty, string[] prompt)
    {
        var state = await LoadServerStateAsync(instanceName);
        
        if (state == null)
        {
            ConsoleUI.PrintError($"Server instance '{instanceName}' not found");
            Console.WriteLine();
            Console.WriteLine("Use 'cic server list' to see available instances");
            return;
        }

        if (!IsContainerRunning(state.ContainerId))
        {
            ConsoleUI.PrintError($"Server instance '{instanceName}' is not running");
            Console.WriteLine();
            Console.WriteLine($"Start it with: cic server start --name {instanceName}");
            return;
        }

        if (!noTty)
        {
            ConsoleUI.PrintInfo($"Connecting to server instance: {instanceName}");
            Console.WriteLine();
        }

        // Get current directory for workspace context
        var currentDir = Directory.GetCurrentDirectory();

        // Get runtime
        var runtime = ContainerRunner.GetRuntime();
        
        // Build command to run in container
        var copilotCommand = new List<string> { "copilot" };

        // Add prompt if provided
        if (prompt.Length > 0)
        {
            if (noTty)
            {
                // For non-interactive mode, use -p/--prompt flag
                copilotCommand.Add("-p");
                copilotCommand.Add(string.Join(" ", prompt));
            }
            else
            {
                // For interactive mode, pass arguments directly
                copilotCommand.AddRange(prompt);
            }
        }
        
        // Build exec arguments
        var args = runtime.BuildExecArguments(
            state.ContainerId,
            "/workspace",
            !noTty,
            copilotCommand.ToArray()
        );

        // Run the command
        if (noTty)
        {
            // For non-interactive mode, capture output
            var (exitCode, output) = await ContainerRunner.RunCommandAsync(runtime.CommandName, args.ToArray());
            
            if (exitCode != 0)
            {
                ConsoleUI.PrintError("Failed to execute command");
                if (!string.IsNullOrEmpty(output))
                {
                    Console.WriteLine(output);
                }
            }
            else
            {
                Console.WriteLine(output);
            }
        }
        else
        {
            // For interactive mode, run interactively
            var startInfo = new ProcessStartInfo
            {
                FileName = runtime.CommandName,
                UseShellExecute = false
            };
            
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();
        }
    }

    private async Task ShowServerStatusAsync(string instanceName)
    {
        var state = await LoadServerStateAsync(instanceName);
        
        if (state == null)
        {
            ConsoleUI.PrintError($"Server instance '{instanceName}' not found");
            return;
        }

        var isRunning = IsContainerRunning(state.ContainerId);

        Console.WriteLine($"Server Instance: {instanceName}");
        Console.WriteLine();
        Console.WriteLine($"  Status: {(isRunning ? "Running" : "Stopped")}");
        Console.WriteLine($"  Container ID: {state.ContainerId[..12]}");
        Console.WriteLine($"  Container Name: {state.ContainerName}");
        Console.WriteLine($"  Port: {state.Port}");
        Console.WriteLine($"  Workspace: {state.WorkspaceFolder}");
        Console.WriteLine($"  Log Level: {state.LogLevel}");
        if (state.Model != null)
            Console.WriteLine($"  Model: {state.Model}");
        Console.WriteLine($"  Started: {state.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
        if (isRunning)
            Console.WriteLine($"  Uptime: {GetUptime(state.StartedAt)}");
        Console.WriteLine();
    }

    private async Task ShowServerLogsAsync(string instanceName, int? tail, bool follow)
    {
        var state = await LoadServerStateAsync(instanceName);
        
        if (state == null)
        {
            ConsoleUI.PrintError($"Server instance '{instanceName}' not found");
            return;
        }

        var runtime = ContainerRunner.GetRuntime();
        var args = runtime.BuildLogsArguments(state.ContainerId, tail, follow);

        if (follow)
        {
            // Run interactively for follow mode
            var startInfo = new ProcessStartInfo
            {
                FileName = runtime.CommandName,
                UseShellExecute = false
            };
            
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = new Process { StartInfo = startInfo };
            process.Start();
            await process.WaitForExitAsync();
        }
        else
        {
            // Get logs and display
            var (exitCode, logs) = await ContainerRunner.RunCommandAsync(runtime.CommandName, args.ToArray());
            
            if (exitCode != 0)
            {
                ConsoleUI.PrintError("Failed to get logs");
                return;
            }

            Console.WriteLine(logs);
        }
    }

    private bool IsContainerRunning(string containerId)
    {
        var runtime = ContainerRunner.GetRuntime();
        return runtime.IsContainerRunning(containerId);
    }

    private string GetUptime(DateTime startedAt)
    {
        var uptime = DateTime.UtcNow - startedAt;
        
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        if (uptime.TotalMinutes >= 1)
            return $"{(int)uptime.TotalMinutes}m {uptime.Seconds}s";
        return $"{(int)uptime.TotalSeconds}s";
    }

    private async Task<ServerState?> LoadServerStateAsync(string instanceName)
    {
        var stateFile = GetServerStateFile(instanceName);
        
        if (!File.Exists(stateFile))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(stateFile);
            return JsonSerializer.Deserialize(json, ServerStateJsonContext.Default.ServerState);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveServerStateAsync(ServerState state)
    {
        var stateFile = GetServerStateFile(state.InstanceName);
        var json = JsonSerializer.Serialize(state, ServerStateJsonContext.Default.ServerState);
        await File.WriteAllTextAsync(stateFile, json);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ServerState))]
internal partial class ServerStateJsonContext : JsonSerializerContext
{
}

internal class ServerState
{
    public string InstanceName { get; set; } = "";
    public string ContainerId { get; set; } = "";
    public string ContainerName { get; set; } = "";
    public int Port { get; set; }
    public string? Model { get; set; }
    public string LogLevel { get; set; } = "info";
    public DateTime StartedAt { get; set; }
    public string WorkspaceFolder { get; set; } = "";
    public string? McpConfigPath { get; set; }
}
