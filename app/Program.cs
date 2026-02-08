using System.CommandLine;
using CopilotInContainer.Commands;
using CopilotInContainer.Infrastructure;

namespace CopilotInContainer;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("GitHub Copilot CLI in Apple Container")
        {
            Description = "A wrapper to run GitHub Copilot CLI in an isolated Apple container"
        };

        // Register model management commands
        var modelCommands = new ModelCommands();
        modelCommands.Configure(rootCommand);

        // Register agent management commands
        var agentCommands = new AgentCommands();
        agentCommands.Configure(rootCommand);

        // Register server management commands
        var serverCommands = new ServerCommands();
        serverCommands.Configure(rootCommand);

        // Register runtime management commands
        var runtimeCommands = new RuntimeCommands();
        runtimeCommands.Configure(rootCommand);

        // Register MCP management commands
        var mcpCommands = new McpCommands();
        mcpCommands.Configure(rootCommand);

        var noPullOption = new Option<bool>(
            "--no-pull",
            "Skip pulling the latest image"
        );

        var modelOption = new Option<string?>(
            "--model",
            "AI model to use for this session (overrides configured default)"
        );

        var agentOption = new Option<string?>(
            "--agent",
            "GitHub Copilot agent to use (e.g., refactor-agent, test-agent)"
        );

        var mcpConfigOption = new Option<string?>(
            "--mcp-config",
            "Path to MCP config directory (overrides default and global config)"
        );

        var noMcpInstallOption = new Option<bool>(
            "--no-mcp-install",
            "Skip automatic MCP server dependency installation"
        );

        var promptArgument = new Argument<string[]>(
            "prompt",
            "Prompt to send to GitHub Copilot CLI"
        )
        { Arity = ArgumentArity.ZeroOrMore };

        rootCommand.AddOption(noPullOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(agentOption);
        rootCommand.AddOption(mcpConfigOption);
        rootCommand.AddOption(noMcpInstallOption);
        rootCommand.AddArgument(promptArgument);

        rootCommand.SetHandler(async (bool noPull, string? model, string? agent, string? mcpConfig, bool noMcpInstall, string[] prompt) =>
        {
            await RunContainer(noPull, model, agent, mcpConfig, noMcpInstall, prompt);
        }, noPullOption, modelOption, agentOption, mcpConfigOption, noMcpInstallOption, promptArgument);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task<int> RunContainer(bool skipPull, string? sessionModel, string? agent, string? mcpConfig, bool noMcpInstall, string[] promptArgs)
    {
        ConsoleUI.PrintInfo("Checking prerequisites...");
        Console.WriteLine();

        // Validate prerequisites
        if (!PrerequisiteChecker.CheckContainer())
            return 1;

        if (!PrerequisiteChecker.CheckGitHubCli())
            return 1;

        if (!await PrerequisiteChecker.CheckGitHubAuthAsync())
            return 1;

        Console.WriteLine();
        ConsoleUI.PrintSuccess("All prerequisites satisfied");
        Console.WriteLine();

        // Check for local image unless skipped
        if (!skipPull)
        {
            if (!ContainerRunner.CheckImage())
                return 1;
            Console.WriteLine();
        }
        else
        {
            ConsoleUI.PrintInfo("Skipping image check");
            Console.WriteLine();
        }

        // Run the container
        return await ContainerRunner.RunAsync(promptArgs, sessionModel, agent, mcpConfig, !noMcpInstall);
    }
}
