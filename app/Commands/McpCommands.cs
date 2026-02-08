using System.CommandLine;
using CopilotInContainer.Infrastructure;

namespace CopilotInContainer.Commands;

/// <summary>
/// Commands for managing MCP (Model Context Protocol) configuration.
/// </summary>
public class McpCommands : ICommand
{
    private const string LocalMcpDir = ".copilot-in-container/mcp";
    private const string McpConfigFile = "mcp-config.json";

    public void Configure(RootCommand rootCommand)
    {
        var mcpCommand = new Command("mcp", "Manage MCP (Model Context Protocol) configuration");

        // Show current MCP config path command
        var showCommand = new Command("show", "Show current MCP configuration path");
        showCommand.SetHandler(() =>
        {
            ShowMcpConfig();
        });

        // Set MCP config path (global)
        var setCommand = new Command("set-path", "Set MCP config directory path (global)");
        var pathArg = new Argument<string>("path", "Path to directory containing mcp-config.json");
        setCommand.AddArgument(pathArg);
        setCommand.SetHandler((string path) =>
        {
            SetMcpPath(path);
        }, pathArg);

        // Clear MCP config path
        var clearCommand = new Command("clear-path", "Clear global MCP config directory path");
        clearCommand.SetHandler(() =>
        {
            ClearMcpPath();
        });

        // Initialize local MCP directory
        var initCommand = new Command("init", "Initialize local MCP directory with sample config");
        initCommand.SetHandler(() =>
        {
            InitializeMcpDirectory();
        });

        mcpCommand.AddCommand(showCommand);
        mcpCommand.AddCommand(setCommand);
        mcpCommand.AddCommand(clearCommand);
        mcpCommand.AddCommand(initCommand);

        rootCommand.AddCommand(mcpCommand);
    }

    private void ShowMcpConfig()
    {
        Console.WriteLine("🔌 MCP Configuration:");
        Console.WriteLine();

        var globalPath = GetGlobalMcpPath();
        var localPath = GetLocalMcpPath();
        var effectivePath = GetEffectiveMcpConfigPath();

        if (!string.IsNullOrEmpty(globalPath))
            Console.WriteLine($"  🌍 Global config path: {globalPath}");
        else
            Console.WriteLine("  🌍 Global config path: (not set)");

        Console.WriteLine($"  📍 Local default: {localPath}");
        Console.WriteLine();

        if (effectivePath != null)
        {
            Console.WriteLine($"✅ Using: {effectivePath}");
            
            var config = McpConfig.LoadFromFile(effectivePath);
            if (config?.McpServers != null && config.McpServers.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Found {config.McpServers.Count} MCP server(s):");
                foreach (var (serverName, server) in config.McpServers)
                {
                    Console.WriteLine($"  • {serverName}");
                    if (!string.IsNullOrEmpty(server.Command))
                        Console.WriteLine($"    Command: {server.Command}");
                    if (!string.IsNullOrEmpty(server.Cwd))
                        Console.WriteLine($"    Working dir: {server.Cwd}");
                }
            }
        }
        else
        {
            ConsoleUI.PrintWarning("No MCP config found");
            Console.WriteLine();
            Console.WriteLine("To initialize:");
            Console.WriteLine("  cic mcp init");
        }
    }

    private void SetMcpPath(string path)
    {
        // Expand ~ to home directory
        if (path.StartsWith("~/"))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2));
        }

        // Make absolute if relative
        if (!Path.IsPathRooted(path))
        {
            path = Path.GetFullPath(path);
        }

        // Check if directory exists
        if (!Directory.Exists(path))
        {
            ConsoleUI.PrintError($"Directory does not exist: {path}");
            return;
        }

        // Check if mcp-config.json exists in the directory
        var configPath = Path.Combine(path, McpConfigFile);
        if (!File.Exists(configPath))
        {
            ConsoleUI.PrintWarning($"Warning: {McpConfigFile} not found in {path}");
            Console.WriteLine("The directory will be set, but MCP won't work until you add mcp-config.json");
        }

        GlobalConfig.SetMcpPath(path);

        ConsoleUI.PrintSuccess($"MCP config path set to: {path}");
    }

    private void ClearMcpPath()
    {
        if (GlobalConfig.ClearMcpPath())
        {
            ConsoleUI.PrintSuccess("Cleared global MCP config path");
        }
        else
        {
            ConsoleUI.PrintWarning("No global MCP config path to clear");
        }
    }

    private void InitializeMcpDirectory()
    {
        var localPath = GetLocalMcpPath();
        var mcpDir = Path.GetDirectoryName(localPath)!;

        // Create directory
        Directory.CreateDirectory(mcpDir);

        if (File.Exists(localPath))
        {
            ConsoleUI.PrintWarning($"MCP config already exists at: {localPath}");
            return;
        }

        // Create sample config
        var sampleConfig = @"{
  ""mcpServers"": {
    ""example-server"": {
      ""command"": ""node"",
      ""args"": [""path/to/your/mcp-server.js""],
      ""cwd"": ""path/to/server/directory"",
      ""env"": {
        ""API_KEY"": ""your-api-key-here""
      }
    }
  }
}";

        File.WriteAllText(localPath, sampleConfig);

        ConsoleUI.PrintSuccess($"Created sample MCP config at: {localPath}");
        Console.WriteLine();
        Console.WriteLine("Edit this file to add your MCP servers.");
        Console.WriteLine("Learn more: https://docs.github.com/en/copilot/how-tos/use-copilot-agents/coding-agent/extend-coding-agent-with-mcp");
    }

    /// <summary>
    /// Gets the effective MCP config path, checking in order:
    /// 1. Global config path setting
    /// 2. Local repo default path
    /// Returns null if no config file is found.
    /// </summary>
    public static string? GetEffectiveMcpConfigPath(string? cliOverride = null)
    {
        // CLI override takes precedence
        if (!string.IsNullOrEmpty(cliOverride))
        {
            var cliPath = ExpandPath(cliOverride);
            if (File.Exists(cliPath))
                return cliPath;
        }

        // Check global config path setting
        var globalPath = GetGlobalMcpPath();
        if (!string.IsNullOrEmpty(globalPath))
        {
            var globalConfigPath = Path.Combine(globalPath, McpConfigFile);
            if (File.Exists(globalConfigPath))
                return globalConfigPath;
        }

        // Check local repo default
        var localPath = GetLocalMcpPath();
        if (File.Exists(localPath))
            return localPath;

        return null;
    }

    private static string GetLocalMcpPath()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), LocalMcpDir, McpConfigFile);
    }

    private static string? GetGlobalMcpPath()
    {
        return GlobalConfig.GetMcpPath();
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2));
        }

        if (!Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return path;
    }
}
