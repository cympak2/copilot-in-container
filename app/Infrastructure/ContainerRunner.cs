using System.Diagnostics;
using CopilotInContainer.Commands;

namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Handles running containers with Apple container runtime.
/// </summary>
public static class ContainerRunner
{
    private const string ImageName = "ghcr.io/cympak2/copilot-in-container:latest";
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "gh-copilot"
    );

    public static bool CheckImage()
    {
        ConsoleUI.PrintInfo($"Checking for image: {ImageName}");

        var (exitCode, output) = RunCommand("container", "image", "ls");

        if (exitCode != 0)
        {
            ConsoleUI.PrintError("Failed to list container images");
            return false;
        }

        // Parse output to check if image exists
        // Format: REPOSITORY TAG IMAGE_ID CREATED SIZE
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var imageParts = ImageName.Split(':');
        var imgName = imageParts[0];
        var imgTag = imageParts.Length > 1 ? imageParts[1] : "latest";

        foreach (var line in lines.Skip(1)) // Skip header
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0] == imgName && parts[1] == imgTag)
            {
                ConsoleUI.PrintSuccess("Image found locally");
                return true;
            }
        }

        ConsoleUI.PrintWarning("Image not found locally. Pulling from registry...");
        Console.WriteLine();
        
        // Pull the image
        ConsoleUI.PrintInfo($"Pulling image: {ImageName}");
        var (pullExitCode, pullOutput) = RunCommand("container", "image", "pull", ImageName);
        
        if (pullExitCode != 0)
        {
            ConsoleUI.PrintError("Failed to pull image");
            Console.WriteLine();
            Console.WriteLine("You can build the image locally with:");
            Console.WriteLine("  ./build.sh");
            Console.WriteLine();
            Console.WriteLine("Or manually:");
            Console.WriteLine($"  container build -t {ImageName} .");
            return false;
        }
        
        ConsoleUI.PrintSuccess("Image pulled successfully");
        return true;
    }

    public static async Task<int> RunAsync(string[] promptArgs, string? sessionModel = null, string? agent = null)
    {
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

        // Build container arguments
        var args = new List<string>
        {
            "run",
            "--rm",
            "--name", containerName,
            "-it",
            "--dns", "8.8.8.8",
            "-e", $"PUID={userId}",
            "-e", $"PGID={groupId}",
            "-e", $"GITHUB_TOKEN={githubToken}",
            "-v", $"{currentDir}:/workspace:rw",
            "-v", $"{ConfigDir}:/home/appuser/.copilot:rw",
            "-w", "/workspace",
            ImageName
        };

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

        return RunInteractive("container", args.ToArray());
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
