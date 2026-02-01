using System.Diagnostics;

namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Checks for required prerequisites to run the application.
/// </summary>
public static class PrerequisiteChecker
{
    public static bool CheckContainer()
    {
        var runtime = ContainerRunner.GetRuntime();
        
        if (!runtime.IsAvailable())
        {
            ConsoleUI.PrintError($"{runtime.DisplayName} is not installed or not available");
            Console.WriteLine();
            Console.WriteLine("Please install one of the following:");
            Console.WriteLine();
            Console.WriteLine("Apple Container:");
            Console.WriteLine("  https://github.com/apple/container/releases");
            Console.WriteLine("  Required: macOS 15+ with Apple silicon");
            Console.WriteLine();
            Console.WriteLine("Docker:");
            Console.WriteLine("  https://docs.docker.com/get-docker/");
            Console.WriteLine();
            Console.WriteLine("After installation, you can set your preference:");
            Console.WriteLine("  cic runtime set --runtime docker");
            Console.WriteLine("  cic runtime set --runtime container");
            return false;
        }

        ConsoleUI.PrintSuccess($"{runtime.DisplayName} found: {runtime.GetVersion()}");
        return true;
    }

    public static bool CheckGitHubCli()
    {
        if (!CommandExists("gh"))
        {
            ConsoleUI.PrintError("GitHub CLI (gh) is not installed");
            Console.WriteLine();
            Console.WriteLine("Install it with:");
            Console.WriteLine("  brew install gh");
            Console.WriteLine();
            Console.WriteLine("Or visit: https://cli.github.com/");
            return false;
        }

        ConsoleUI.PrintSuccess("GitHub CLI found");
        return true;
    }

    public static async Task<bool> CheckGitHubAuthAsync()
    {
        var (exitCode, _) = await RunCommandAsync("gh", "auth", "status");

        if (exitCode != 0)
        {
            ConsoleUI.PrintError("Not authenticated with GitHub CLI");
            Console.WriteLine();
            Console.WriteLine("Please run:");
            Console.WriteLine("  gh auth login");
            return false;
        }

        // Verify we can get a token
        var (tokenExitCode, token) = await RunCommandAsync("gh", "auth", "token");

        if (tokenExitCode != 0 || string.IsNullOrWhiteSpace(token))
        {
            ConsoleUI.PrintError("Could not retrieve GitHub token");
            return false;
        }

        ConsoleUI.PrintSuccess("GitHub authentication verified");
        return true;
    }

    private static bool CommandExists(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "command",
                Arguments = $"-v {command}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<(int exitCode, string output)> RunCommandAsync(string fileName, params string[] arguments)
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

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output.Trim());
        }
        catch (Exception ex)
        {
            ConsoleUI.PrintError($"Error running {fileName}: {ex.Message}");
            return (-1, string.Empty);
        }
    }
}
