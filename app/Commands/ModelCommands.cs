using System.CommandLine;
using System.Text.RegularExpressions;
using CopilotInContainer.Infrastructure;

namespace CopilotInContainer.Commands;

/// <summary>
/// Commands for managing AI model configurations.
/// </summary>
public class ModelCommands : ICommand
{
    private const string LocalConfigDir = ".copilot-in-container";
    private const string GlobalConfigDir = ".config/copilot-in-container";
    private const string ModelConfigFile = "model.conf";
    private const string DefaultKeyword = "default";
    private const string ImageName = "copilot-in-container:latest";

    public void Configure(RootCommand root)
    {
        root.Add(CreateListModelsCommand());
        root.Add(CreateShowModelCommand());
        root.Add(CreateSetModelCommand());
        root.Add(CreateSetModelGlobalCommand());
        root.Add(CreateClearModelCommand());
        root.Add(CreateClearModelGlobalCommand());
    }

    private static Command CreateListModelsCommand()
    {
        var command = new Command("--list-models", "List available AI models from GitHub Copilot CLI");
        
        command.SetHandler(async () =>
        {
            Console.WriteLine("🤖 Fetching available models...");
            Console.WriteLine();

            // Get GitHub token
            var (tokenExitCode, githubToken) = await RunCommandAsync("gh", "auth", "token");
            if (tokenExitCode != 0 || string.IsNullOrWhiteSpace(githubToken))
            {
                ConsoleUI.PrintError("Failed to retrieve GitHub token. Run 'gh auth login' first.");
                Environment.ExitCode = 1;
                return;
            }

            // Run container with invalid model to trigger error that lists models
            var args = new List<string>
            {
                "run",
                "--rm",
                "-e", $"GITHUB_TOKEN={githubToken}",
                ImageName,
                "copilot",
                "--model", "invalid-model-to-trigger-list"
            };

            var (exitCode, output) = await RunCommandCaptureAsync("container", args.ToArray());

            // Parse model list from error output
            var models = ParseModelListFromError(output);

            if (models.Count == 0)
            {
                ConsoleUI.PrintError("Could not parse model list from Copilot CLI output");
                Console.WriteLine();
                Console.WriteLine("Raw error output:");
                Console.WriteLine(output);
                Environment.ExitCode = 1;
                return;
            }

            Console.WriteLine("Available models:");
            foreach (var model in models)
            {
                Console.WriteLine($"  • {model}");
            }
            
            Console.WriteLine();
            Console.WriteLine("💡 Set your preferred model:");
            Console.WriteLine("   copilot-in-container --set-model <model-id>        (current project)");
            Console.WriteLine("   cic --set-model <model-id>                         (current project)");
            Console.WriteLine("   cic --set-model-global <model-id>                  (all projects)");
        });

        return command;
    }

    private static Command CreateShowModelCommand()
    {
        var command = new Command("--show-model", "Show current model configuration");

        command.SetHandler(() =>
        {
            var localPath = GetLocalConfigPath();
            var globalPath = GetGlobalConfigPath();

            var localModel = ConfigFile.ReadValue(localPath);
            var globalModel = ConfigFile.ReadValue(globalPath);

            // Normalize "default" keyword
            if (string.Equals(localModel, DefaultKeyword, StringComparison.OrdinalIgnoreCase))
                localModel = null;
            if (string.Equals(globalModel, DefaultKeyword, StringComparison.OrdinalIgnoreCase))
                globalModel = null;

            Console.WriteLine("🤖 Model Configuration:");
            Console.WriteLine();

            if (localModel is not null)
                Console.WriteLine($"  📍 Local config (.copilot-in-container/model.conf): {localModel}");
            else
                Console.WriteLine("  📍 Local config: (not set)");

            if (globalModel is not null)
                Console.WriteLine($"  🌍 Global config (~/.config/copilot-in-container/model.conf): {globalModel}");
            else
                Console.WriteLine("  🌍 Global config: (not set)");

            Console.WriteLine();
            Console.WriteLine("  🏭 Application default: (GitHub Copilot CLI default)");
            Console.WriteLine();

            // Show effective model
            var effectiveModel = localModel ?? globalModel;
            if (effectiveModel is not null)
                Console.WriteLine($"✅ Currently using: {effectiveModel}");
            else
                Console.WriteLine("ℹ️  Currently using: GitHub Copilot CLI default");
        });

        return command;
    }

    private static Command CreateSetModelCommand()
    {
        var command = new Command("--set-model", "Set default model in local config (current project)");
        
        var modelArg = new Argument<string>("model", "Model ID to set (e.g., gpt-4o, claude-3.5-sonnet)");
        command.Add(modelArg);

        command.SetHandler((string model) =>
        {
            var path = GetLocalConfigPath();
            ConfigFile.WriteValue(path, model);
            ConsoleUI.PrintSuccess($"Set local model: {model}");
            Console.WriteLine($"  Config: {path}");
        }, modelArg);

        return command;
    }

    private static Command CreateSetModelGlobalCommand()
    {
        var command = new Command("--set-model-global", "Set default model in global config (all projects)");
        
        var modelArg = new Argument<string>("model", "Model ID to set (e.g., gpt-4o, claude-3.5-sonnet)");
        command.Add(modelArg);

        command.SetHandler((string model) =>
        {
            var path = GetGlobalConfigPath();
            ConfigFile.WriteValue(path, model);
            ConsoleUI.PrintSuccess($"Set global model: {model}");
            Console.WriteLine($"  Config: {path}");
        }, modelArg);

        return command;
    }

    private static Command CreateClearModelCommand()
    {
        var command = new Command("--clear-model", "Clear local model configuration");

        command.SetHandler(() =>
        {
            var path = GetLocalConfigPath();
            if (ConfigFile.Delete(path))
            {
                ConsoleUI.PrintSuccess("Cleared local model configuration");
            }
            else
            {
                ConsoleUI.PrintWarning("No local model configuration to clear");
            }
        });

        return command;
    }

    private static Command CreateClearModelGlobalCommand()
    {
        var command = new Command("--clear-model-global", "Clear global model configuration");

        command.SetHandler(() =>
        {
            var path = GetGlobalConfigPath();
            if (ConfigFile.Delete(path))
            {
                ConsoleUI.PrintSuccess("Cleared global model configuration");
            }
            else
            {
                ConsoleUI.PrintWarning("No global model configuration to clear");
            }
        });

        return command;
    }

    // Helper methods

    private static string GetLocalConfigPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        return Path.Combine(currentDir, LocalConfigDir, ModelConfigFile);
    }

    private static string GetGlobalConfigPath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, GlobalConfigDir, ModelConfigFile);
    }

    private static List<string> ParseModelListFromError(string errorOutput)
    {
        var models = new List<string>();

        // Look for "Allowed choices are model1, model2, model3."
        var match = Regex.Match(errorOutput, @"Allowed choices are\s+(.+?)\.(?:\s|$)", 
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        if (match.Success)
        {
            var modelString = match.Groups[1].Value;
            var parts = modelString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            foreach (var part in parts)
            {
                var cleaned = part.Trim();
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    models.Add(cleaned);
                }
            }
        }

        return models;
    }

    private static async Task<(int exitCode, string output)> RunCommandAsync(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return (-1, string.Empty);

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, output.Trim());
        }
        catch
        {
            return (-1, string.Empty);
        }
    }

    private static async Task<(int exitCode, string output)> RunCommandCaptureAsync(string fileName, params string[] arguments)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return (-1, string.Empty);

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Combine stdout and stderr for parsing
            var output = string.IsNullOrEmpty(stderr) ? stdout : stderr;

            return (process.ExitCode, output.Trim());
        }
        catch
        {
            return (-1, string.Empty);
        }
    }

    /// <summary>
    /// Gets the configured model (local or global) to use in container runs.
    /// Returns null if no model is configured (use default).
    /// </summary>
    public static string? GetConfiguredModel()
    {
        var localPath = GetLocalConfigPath();
        var globalPath = GetGlobalConfigPath();

        var localModel = ConfigFile.ReadValue(localPath);
        var globalModel = ConfigFile.ReadValue(globalPath);

        // Normalize "default" keyword
        if (string.Equals(localModel, DefaultKeyword, StringComparison.OrdinalIgnoreCase))
            localModel = null;
        if (string.Equals(globalModel, DefaultKeyword, StringComparison.OrdinalIgnoreCase))
            globalModel = null;

        // Local takes precedence over global
        return localModel ?? globalModel;
    }
}
