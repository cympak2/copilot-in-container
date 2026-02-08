using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Represents the structure of an MCP (Model Context Protocol) configuration file.
/// </summary>
public class McpConfig
{
    [JsonPropertyName("mcpServers")]
    public Dictionary<string, McpServer>? McpServers { get; set; }

    public static McpConfig? LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, McpConfigJsonContext.Default.McpConfig);
        }
        catch (Exception ex)
        {
            ConsoleUI.PrintWarning($"Failed to parse MCP config: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets all unique directories referenced by MCP servers for dependency installation.
    /// </summary>
    public List<string> GetServerDirectories()
    {
        var directories = new List<string>();

        if (McpServers == null)
            return directories;

        foreach (var (serverName, server) in McpServers)
        {
            // Prefer explicit cwd
            if (!string.IsNullOrEmpty(server.Cwd))
            {
                directories.Add(server.Cwd);
                continue;
            }

            // Try to derive from command path
            if (!string.IsNullOrEmpty(server.Command))
            {
                var commandDir = Path.GetDirectoryName(server.Command);
                if (!string.IsNullOrEmpty(commandDir) && commandDir != ".")
                {
                    directories.Add(commandDir);
                }
            }

            // Try to derive from first arg if it's a file path
            if (server.Args?.Length > 0)
            {
                var firstArg = server.Args[0];
                if (firstArg.EndsWith(".js") || firstArg.EndsWith(".py") || 
                    firstArg.EndsWith(".mjs") || firstArg.Contains("/") || firstArg.Contains("\\"))
                {
                    var argDir = Path.GetDirectoryName(firstArg);
                    if (!string.IsNullOrEmpty(argDir) && argDir != ".")
                    {
                        directories.Add(argDir);
                    }
                }
            }
        }

        return directories.Distinct().ToList();
    }
}

/// <summary>
/// Represents a single MCP server configuration.
/// </summary>
public class McpServer
{
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("args")]
    public string[]? Args { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string>? Env { get; set; }

    /// <summary>
    /// Determines if this server is a Node.js server.
    /// </summary>
    public bool IsNodeServer()
    {
        if (string.IsNullOrEmpty(Command))
            return false;

        var cmd = Command.ToLowerInvariant();
        return cmd == "node" || cmd == "nodejs" || cmd.EndsWith("/node") || cmd.EndsWith("/nodejs");
    }

    /// <summary>
    /// Determines if this server is a Python server.
    /// </summary>
    public bool IsPythonServer()
    {
        if (string.IsNullOrEmpty(Command))
            return false;

        var cmd = Command.ToLowerInvariant();
        return cmd == "python" || cmd == "python3" || cmd.EndsWith("/python") || cmd.EndsWith("/python3");
    }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
)]
[JsonSerializable(typeof(McpConfig))]
[JsonSerializable(typeof(McpServer))]
[JsonSerializable(typeof(Dictionary<string, McpServer>))]
internal partial class McpConfigJsonContext : JsonSerializerContext
{
}
