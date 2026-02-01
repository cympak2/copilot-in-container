using System.CommandLine;
using CopilotInContainer.Infrastructure;

namespace CopilotInContainer.Commands;

/// <summary>
/// Commands for managing container runtime configuration.
/// </summary>
public class RuntimeCommands : ICommand
{
    public void Configure(RootCommand rootCommand)
    {
        var runtimeCommand = new Command("runtime", "Manage container runtime configuration");

        // Show command
        var showCommand = new Command("show", "Show current runtime configuration");
        showCommand.SetHandler(() =>
        {
            ShowRuntime();
        });

        // Set command
        var setCommand = new Command("set", "Set preferred container runtime");
        var runtimeOption = new Option<string>(
            "--runtime",
            description: "Runtime to use (docker or container)"
        ) { IsRequired = true };
        setCommand.AddOption(runtimeOption);
        setCommand.SetHandler((string runtime) =>
        {
            SetRuntime(runtime);
        }, runtimeOption);

        // List available runtimes command
        var listCommand = new Command("list", "List available container runtimes");
        listCommand.SetHandler(() =>
        {
            ListAvailableRuntimes();
        });

        runtimeCommand.AddCommand(showCommand);
        runtimeCommand.AddCommand(setCommand);
        runtimeCommand.AddCommand(listCommand);
        
        rootCommand.AddCommand(runtimeCommand);
    }

    private void ShowRuntime()
    {
        var runtime = ContainerRunner.GetRuntime();
        
        Console.WriteLine("Current Runtime Configuration:");
        Console.WriteLine();
        Console.WriteLine($"  Runtime: {runtime.DisplayName}");
        Console.WriteLine($"  Command: {runtime.CommandName}");
        Console.WriteLine($"  Available: {(runtime.IsAvailable() ? "Yes" : "No")}");
        
        if (runtime.IsAvailable())
        {
            Console.WriteLine($"  Version: {runtime.GetVersion()}");
        }
        
        Console.WriteLine();
    }

    private void SetRuntime(string runtimeName)
    {
        var normalizedName = runtimeName.ToLowerInvariant();
        
        IContainerRuntime runtime = normalizedName switch
        {
            "docker" => new DockerRuntime(),
            "container" => new AppleContainerRuntime(),
            _ => null!
        };

        if (runtime == null)
        {
            ConsoleUI.PrintError($"Unknown runtime: {runtimeName}");
            Console.WriteLine();
            Console.WriteLine("Available runtimes:");
            Console.WriteLine("  - docker");
            Console.WriteLine("  - container");
            return;
        }

        if (!runtime.IsAvailable())
        {
            ConsoleUI.PrintError($"{runtime.DisplayName} is not available on this system");
            Console.WriteLine();
            Console.WriteLine("Install instructions:");
            if (runtime is DockerRuntime)
            {
                Console.WriteLine("  Docker: https://docs.docker.com/get-docker/");
            }
            else if (runtime is AppleContainerRuntime)
            {
                Console.WriteLine("  Apple Container: https://github.com/apple/container/releases");
                Console.WriteLine("  Requires: macOS 15+");
            }
            return;
        }

        ContainerRunner.SetRuntime(normalizedName);
        
        ConsoleUI.PrintSuccess($"Runtime set to: {runtime.DisplayName}");
        Console.WriteLine();
        Console.WriteLine($"  Command: {runtime.CommandName}");
        Console.WriteLine($"  Version: {runtime.GetVersion()}");
        Console.WriteLine();
    }

    private void ListAvailableRuntimes()
    {
        Console.WriteLine("Available Container Runtimes:");
        Console.WriteLine();
        Console.WriteLine("RUNTIME            AVAILABLE  VERSION");
        Console.WriteLine("─────────────────  ─────────  ────────────────────────────");

        var runtimes = new IContainerRuntime[]
        {
            new AppleContainerRuntime(),
            new DockerRuntime()
        };

        foreach (var runtime in runtimes)
        {
            var available = runtime.IsAvailable();
            var version = available ? runtime.GetVersion() : "not installed";
            var availableStr = available ? "✓" : "✗";
            
            Console.WriteLine($"{runtime.DisplayName,-17}  {availableStr,-9}  {version}");
        }

        Console.WriteLine();
        Console.WriteLine("To set your preferred runtime:");
        Console.WriteLine("  cic runtime set --runtime docker");
        Console.WriteLine("  cic runtime set --runtime container");
        Console.WriteLine();
    }
}
