using System.CommandLine;
using CopilotInContainer.Infrastructure;

namespace CopilotInContainer.Commands;

/// <summary>
/// Commands for managing GitHub Copilot agents.
/// </summary>
public class AgentCommands : ICommand
{
    private const string AgentsDirectory = ".github/agents";

    public void Configure(RootCommand root)
    {
        root.Add(CreateListAgentsCommand());
    }

    private static Command CreateListAgentsCommand()
    {
        var command = new Command("--list-agents", "List available GitHub Copilot agents in .github/agents");

        command.SetHandler(() =>
        {
            var currentDir = Directory.GetCurrentDirectory();
            var agentsPath = AgentDiscovery.GetAgentsDirectoryPath(currentDir);

            Console.WriteLine("🤖 GitHub Copilot Agents:");
            Console.WriteLine();

            if (!Directory.Exists(agentsPath))
            {
                ConsoleUI.PrintWarning($"No agents directory found at {AgentsDirectory}");
                Console.WriteLine();
                Console.WriteLine("To create agents:");
                Console.WriteLine($"  1. Create directory: mkdir -p {AgentsDirectory}");
                Console.WriteLine($"  2. Add agent files (*.md or *.yml)");
                Console.WriteLine();
                Console.WriteLine("Learn more: https://github.com/features/copilot");
                return;
            }
            var agents = AgentDiscovery.FindAgentsInWorkspace(currentDir);

            if (agents.Count == 0)
            {
                ConsoleUI.PrintWarning($"No agent files found in {AgentsDirectory}");
                Console.WriteLine();
                Console.WriteLine("Supported formats: .md, .yml, .yaml, .json");
                return;
            }

            Console.WriteLine($"Found {agents.Count} agent(s) in {AgentsDirectory}:");
            Console.WriteLine();

            foreach (var agent in agents)
            {
                var sizeStr = AgentDiscovery.FormatFileSize(agent.FileSizeBytes);

                Console.WriteLine($"  • {agent.FileName}");
                Console.WriteLine($"    Agent name: {agent.Name}");
                Console.WriteLine($"    Size: {sizeStr}");

                if (!string.IsNullOrEmpty(agent.Description))
                {
                    Console.WriteLine($"    Description: {agent.Description}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("💡 Use an agent:");
            Console.WriteLine($"   copilot-in-container --agent=<agent-name> \"Your prompt\"");
            Console.WriteLine($"   cic --agent=<agent-name> \"Your prompt\"");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine($"   cic --agent={agents[0].Name} \"Refactor this code\"");
        });

        return command;
    }
}
