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
            var agentsPath = Path.Combine(currentDir, AgentsDirectory);

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

            // Find all agent files (commonly .md or .yml files in .github/agents)
            var agentFiles = Directory.GetFiles(agentsPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => 
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".md" || ext == ".yml" || ext == ".yaml" || ext == ".json";
                })
                .OrderBy(f => Path.GetFileName(f))
                .ToList();

            if (agentFiles.Count == 0)
            {
                ConsoleUI.PrintWarning($"No agent files found in {AgentsDirectory}");
                Console.WriteLine();
                Console.WriteLine("Supported formats: .md, .yml, .yaml, .json");
                return;
            }

            Console.WriteLine($"Found {agentFiles.Count} agent(s) in {AgentsDirectory}:");
            Console.WriteLine();

            foreach (var agentFile in agentFiles)
            {
                var fileName = Path.GetFileName(agentFile);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(agentFile);
                var fileSize = new FileInfo(agentFile).Length;
                var sizeStr = FormatFileSize(fileSize);

                Console.WriteLine($"  • {fileName}");
                Console.WriteLine($"    Agent name: {fileNameWithoutExt}");
                Console.WriteLine($"    Size: {sizeStr}");

                // Try to read first line for description
                try
                {
                    var firstLine = File.ReadLines(agentFile).FirstOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(firstLine))
                    {
                        // Remove markdown comment markers if present
                        if (firstLine.StartsWith("#"))
                            firstLine = firstLine.TrimStart('#').Trim();
                        if (firstLine.Length > 60)
                            firstLine = firstLine.Substring(0, 57) + "...";
                        Console.WriteLine($"    Description: {firstLine}");
                    }
                }
                catch
                {
                    // Ignore errors reading file
                }

                Console.WriteLine();
            }

            Console.WriteLine("💡 Use an agent:");
            Console.WriteLine($"   copilot-in-container --agent=<agent-name> \"Your prompt\"");
            Console.WriteLine($"   cic --agent=<agent-name> \"Your prompt\"");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine($"   cic --agent={Path.GetFileNameWithoutExtension(agentFiles[0])} \"Refactor this code\"");
        });

        return command;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        else
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
