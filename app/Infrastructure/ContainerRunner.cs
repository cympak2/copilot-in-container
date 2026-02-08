using System.Diagnostics;
using CopilotInContainer.Commands;

namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Handles running containers with various container runtimes.
/// </summary>
public static class ContainerRunner
{
    private const string ImageName = "ghcr.io/cympak2/copilot-in-container:latest";
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "gh-copilot"
    );
    
    private static IContainerRuntime? _runtime;
    
    /// <summary>
    /// Gets the configured container runtime, or auto-detects if not configured.
    /// </summary>
    public static IContainerRuntime GetRuntime()
    {
        if (_runtime != null)
            return _runtime;
            
        // Try to load from config
        var configuredRuntime = GlobalConfig.GetRuntime();
        
        if (!string.IsNullOrEmpty(configuredRuntime))
        {
            _runtime = configuredRuntime.ToLowerInvariant() switch
            {
                "docker" => new DockerRuntime(),
                "container" => new AppleContainerRuntime(),
                "podman" => new PodmanRuntime(),
                _ => null
            };
            
            if (_runtime?.IsAvailable() == true)
                return _runtime;
        }
        
        // Auto-detect: prefer Apple Container on macOS, then Podman, then Docker
        var appleContainer = new AppleContainerRuntime();
        if (appleContainer.IsAvailable())
        {
            _runtime = appleContainer;
            return _runtime;
        }
        
        var podman = new PodmanRuntime();
        if (podman.IsAvailable())
        {
            _runtime = podman;
            return _runtime;
        }
        
        var docker = new DockerRuntime();
        if (docker.IsAvailable())
        {
            _runtime = docker;
            return _runtime;
        }
        
        // Fallback to Docker (will fail later with proper error)
        _runtime = new DockerRuntime();
        return _runtime;
    }
    
    /// <summary>
    /// Sets the container runtime to use.
    /// </summary>
    public static void SetRuntime(string runtimeName)
    {
        GlobalConfig.SetRuntime(runtimeName);
        _runtime = null; // Force reload
    }

    public static bool CheckImage()
    {
        var runtime = GetRuntime();
        
        ConsoleUI.PrintInfo($"Checking for image: {ImageName}");

        if (!runtime.IsAvailable())
        {
            ConsoleUI.PrintError($"{runtime.DisplayName} is not available");
            return false;
        }

        if (runtime.CheckImageExists(ImageName))
        {
            ConsoleUI.PrintSuccess("Image found locally");
            return true;
        }

        ConsoleUI.PrintWarning("Image not found locally. Pulling from registry...");
        Console.WriteLine();
        
        // Pull the image
        ConsoleUI.PrintInfo($"Pulling image: {ImageName}");
        var (pullExitCode, pullOutput) = runtime.PullImage(ImageName);
        
        if (pullExitCode != 0)
        {
            ConsoleUI.PrintError("Failed to pull image");
            Console.WriteLine();
            Console.WriteLine("You can build the image locally with:");
            Console.WriteLine("  ./build.sh");
            Console.WriteLine();
            Console.WriteLine("Or manually:");
            Console.WriteLine($"  {runtime.CommandName} build -t {ImageName} .");
            return false;
        }
        
        ConsoleUI.PrintSuccess("Image pulled successfully");
        return true;
    }

    public static async Task<int> RunAsync(string[] promptArgs, string? sessionModel = null, string? agent = null, string? mcpConfigOverride = null, bool installMcpDeps = true)
    {
        var runtime = GetRuntime();
        
        // Ensure config directory exists
        Directory.CreateDirectory(ConfigDir);

        // Get GitHub token
        var (tokenExitCode, githubToken) = await RunCommandAsync("gh", "auth", "token");
        if (tokenExitCode != 0 || string.IsNullOrWhiteSpace(githubToken))
        {
            ConsoleUI.PrintError("Failed to retrieve GitHub token");
            return 1;
        }

        // Get current directory
        var currentDir = Directory.GetCurrentDirectory();

        // Get user and group IDs
        var userId = GetUserId();
        var groupId = GetGroupId();

        // Generate unique container name
        var containerName = $"copilot-in-container-{Guid.NewGuid():N}";

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
            { ConfigDir, "/home/appuser/.copilot:rw" }
        };

        // Handle MCP configuration
        var mcpConfigPath = Commands.McpCommands.GetEffectiveMcpConfigPath(mcpConfigOverride);
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
                await InstallMcpDependencies(runtime, mcpConfigPath, volumes, envVars, containerName);
            }
        }
        else
        {
            // Check if user has MCP config in default location but didn't set it up
            var defaultUserMcpConfig = Path.Combine(ConfigDir, "mcp-config.json");
            if (File.Exists(defaultUserMcpConfig) && mcpConfigOverride == null)
            {
                ConsoleUI.PrintWarning("MCP config found at ~/.copilot/mcp-config.json but not in repository");
                Console.WriteLine("To use MCP servers, either:");
                Console.WriteLine("  1. Initialize local config: cic mcp init");
                Console.WriteLine("  2. Set global path: cic mcp set-path ~/.copilot");
                Console.WriteLine("  3. Use CLI option: cic --mcp-config ~/.copilot");
                Console.WriteLine();
            }
        }

        // Build container arguments
        var args = runtime.BuildRunArguments(
            ImageName,
            containerName,
            envVars,
            volumes,
            "/workspace",
            interactive: true,
            removeOnExit: true
        );

        // Add copilot command if needed
        bool copilotAdded = false;
        
        // Add agent if specified
        if (agent is not null)
        {
            args.Add("copilot");
            args.Add($"--agent={agent}");
            copilotAdded = true;
            Console.WriteLine($"🤖 Using agent: {agent}");
        }

        // Add prompt arguments if provided
        if (promptArgs.Length > 0)
        {
            if (!copilotAdded)
            {
                args.Add("copilot");
                copilotAdded = true;
            }
            args.Add("--prompt");
            args.AddRange(promptArgs);
        }

        // Determine which model to use: session model takes precedence over configured model
        var modelToUse = sessionModel ?? ModelCommands.GetConfiguredModel();
        if (modelToUse is not null)
        {
            // If copilot command not yet added, add it
            if (!copilotAdded)
            {
                args.Add("copilot");
                copilotAdded = true;
            }
            args.Add("--model");
            args.Add(modelToUse);
            
            if (sessionModel is not null)
                Console.WriteLine($"🤖 Using model (session): {modelToUse}");
            else
                Console.WriteLine($"🤖 Using model (configured): {modelToUse}");
        }

        // Add MCP config if available
        if (mcpContainerPath != null)
        {
            if (!copilotAdded)
            {
                args.Add("copilot");
                copilotAdded = true;
            }
            args.Add("--additional-mcp-config");
            args.Add($"{mcpContainerPath}/mcp-config.json");
        }

        // Run the container
        Console.WriteLine("🚀 Starting GitHub Copilot CLI...");
        Console.WriteLine();
        
        return RunInteractive(runtime.CommandName, args.ToArray());
    }

    public static string GetUserId()
    {
        var (exitCode, output) = RunCommand("id", "-u");
        return (exitCode == 0 && int.TryParse(output, out var id)) ? id.ToString() : "1000";
    }

    public static string GetGroupId()
    {
        var (exitCode, output) = RunCommand("id", "-g");
        return (exitCode == 0 && int.TryParse(output, out var id)) ? id.ToString() : "1000";
    }

    /// <summary>
    /// Installs dependencies for MCP servers by detecting package.json or Python dependency files.
    /// </summary>
    public static async Task InstallMcpDependencies(
        IContainerRuntime runtime, 
        string mcpConfigPath, 
        Dictionary<string, string> volumes, 
        Dictionary<string, string> envVars, 
        string containerName)
    {
        var config = McpConfig.LoadFromFile(mcpConfigPath);
        if (config?.McpServers == null || config.McpServers.Count == 0)
            return;

        var mcpConfigDir = Path.GetDirectoryName(mcpConfigPath)!;
        var serverDirs = config.GetServerDirectories();

        if (serverDirs.Count == 0)
            return;

        ConsoleUI.PrintInfo("Checking MCP server dependencies...");

        foreach (var relativeDir in serverDirs)
        {
            // Convert relative path to absolute, relative to MCP config directory
            var serverDir = Path.IsPathRooted(relativeDir) 
                ? relativeDir 
                : Path.GetFullPath(Path.Combine(mcpConfigDir, relativeDir));

            if (!Directory.Exists(serverDir))
            {
                ConsoleUI.PrintWarning($"MCP server directory not found: {serverDir}");
                continue;
            }

            // Check for Node.js dependencies
            var packageJsonPath = Path.Combine(serverDir, "package.json");
            var hasNodeDeps = File.Exists(packageJsonPath);

            // Check for Python dependencies
            var requirementsPath = Path.Combine(serverDir, "requirements.txt");
            var pyprojectPath = Path.Combine(serverDir, "pyproject.toml");
            var hasPythonDeps = File.Exists(requirementsPath) || File.Exists(pyprojectPath);

            if (!hasNodeDeps && !hasPythonDeps)
                continue;

            var containerServerPath = $"/mcp/servers/{Path.GetFileName(serverDir)}";
            
            // Temporarily add this directory to volumes for installation
            var volumeKey = serverDir;
            if (!volumes.ContainsKey(volumeKey))
            {
                volumes.Add(volumeKey, $"{containerServerPath}:rw");
            }

            if (hasNodeDeps)
            {
                Console.WriteLine($"  📦 Installing Node.js dependencies in {Path.GetFileName(serverDir)}...");
                await RunDependencyInstall(runtime, envVars, containerName, containerServerPath, "npm", "install");
            }

            if (hasPythonDeps)
            {
                Console.WriteLine($"  🐍 Installing Python dependencies in {Path.GetFileName(serverDir)}...");
                
                if (File.Exists(requirementsPath))
                {
                    await RunDependencyInstall(runtime, envVars, containerName, containerServerPath, "pip", "install", "-r", "requirements.txt");
                }
                else if (File.Exists(pyprojectPath))
                {
                    await RunDependencyInstall(runtime, envVars, containerName, containerServerPath, "pip", "install", "-e", ".");
                }
            }
        }

        Console.WriteLine();
    }

    private static async Task RunDependencyInstall(
        IContainerRuntime runtime,
        Dictionary<string, string> envVars,
        string containerName,
        string workDir,
        string command,
        params string[] args)
    {
        var installEnvVars = new Dictionary<string, string>(envVars);
        var installVolumes = new Dictionary<string, string>();
        
        // We'll use a simple run command for installation
        var installArgs = new List<string>
        {
            "run",
            "--rm",
            "-w", workDir
        };

        // Add environment variables
        foreach (var (key, value) in installEnvVars)
        {
            installArgs.Add("-e");
            installArgs.Add($"{key}={value}");
        }

        // Add the image and command
        installArgs.Add(ImageName);
        installArgs.Add(command);
        installArgs.AddRange(args);

        var (exitCode, output) = await RunCommandAsync(runtime.CommandName, installArgs.ToArray());
        
        if (exitCode != 0)
        {
            ConsoleUI.PrintWarning($"Dependency installation failed (continuing anyway)");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine($"    Error: {output.Split('\n').FirstOrDefault()}");
            }
        }
    }

    public static (int exitCode, string output) RunCommand(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (-1, string.Empty);

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output.Trim());
        }
        catch
        {
            return (-1, string.Empty);
        }
    }

    public static async Task<(int exitCode, string output)> RunCommandAsync(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (-1, string.Empty);

            // Read both stdout and stderr
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            var output = await outputTask;
            var error = await errorTask;
            
            // Combine stdout and stderr, prioritizing error if both exist
            var combinedOutput = string.IsNullOrEmpty(error) ? output : (output + "\n" + error);

            return (process.ExitCode, combinedOutput.Trim());
        }
        catch
        {
            return (-1, string.Empty);
        }
    }

    private static int RunInteractive(string fileName, params string[] arguments)
    {
        try
        {
            // Intercept Ctrl+C to let container handle it
            Console.CancelKeyPress += (_, e) => e.Cancel = true;

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false
            };

            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                ConsoleUI.PrintError($"Failed to start {fileName}");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            ConsoleUI.PrintError($"Error running {fileName}: {ex.Message}");
            return 1;
        }
    }
}
