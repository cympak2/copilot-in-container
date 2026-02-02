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
    
    private static readonly string RuntimeConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "copilot-in-container",
        "runtime"
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
        var configuredRuntime = ConfigFile.ReadValue(RuntimeConfigPath);
        
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
        ConfigFile.WriteValue(RuntimeConfigPath, runtimeName);
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

    public static async Task<int> RunAsync(string[] promptArgs, string? sessionModel = null, string? agent = null)
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
