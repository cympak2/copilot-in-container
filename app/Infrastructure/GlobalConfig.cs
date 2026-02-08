using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Global configuration settings for copilot-in-container.
/// Stored at ~/.config/copilot-in-container/config.json
/// </summary>
public class GlobalConfig
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "copilot-in-container",
        "config.json"
    );

    // Legacy config paths for migration
    private static readonly string LegacyRuntimePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "copilot-in-container",
        "runtime"
    );

    private static readonly string LegacyMcpPathFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "copilot-in-container",
        "mcp-path"
    );

    [JsonPropertyName("runtime")]
    public string? Runtime { get; set; }

    [JsonPropertyName("mcpPath")]
    public string? McpPath { get; set; }

    /// <summary>
    /// Loads the global config, migrating from legacy files if needed.
    /// </summary>
    public static GlobalConfig Load()
    {
        // Try to load from new config file
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize(json, GlobalConfigJsonContext.Default.GlobalConfig) 
                    ?? new GlobalConfig();
            }
            catch
            {
                // If deserialization fails, return new config
                return new GlobalConfig();
            }
        }

        // Migrate from legacy files
        var config = new GlobalConfig();
        bool needsSave = false;

        // Migrate runtime setting
        if (File.Exists(LegacyRuntimePath))
        {
            try
            {
                config.Runtime = File.ReadAllText(LegacyRuntimePath).Trim();
                needsSave = true;
            }
            catch { }
        }

        // Migrate MCP path setting
        if (File.Exists(LegacyMcpPathFile))
        {
            try
            {
                config.McpPath = File.ReadAllText(LegacyMcpPathFile).Trim();
                needsSave = true;
            }
            catch { }
        }

        // Save migrated config and clean up legacy files
        if (needsSave)
        {
            config.Save();
            
            // Remove legacy files after successful migration
            try
            {
                if (File.Exists(LegacyRuntimePath))
                    File.Delete(LegacyRuntimePath);
                if (File.Exists(LegacyMcpPathFile))
                    File.Delete(LegacyMcpPathFile);
            }
            catch { }
        }

        return config;
    }

    /// <summary>
    /// Saves the global config to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, GlobalConfigJsonContext.Default.GlobalConfig);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            ConsoleUI.PrintWarning($"Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the configured runtime, or null if not set.
    /// </summary>
    public static string? GetRuntime()
    {
        return Load().Runtime;
    }

    /// <summary>
    /// Sets the configured runtime.
    /// </summary>
    public static void SetRuntime(string runtime)
    {
        var config = Load();
        config.Runtime = runtime;
        config.Save();
    }

    /// <summary>
    /// Gets the configured MCP path, or null if not set.
    /// </summary>
    public static string? GetMcpPath()
    {
        return Load().McpPath;
    }

    /// <summary>
    /// Sets the configured MCP path.
    /// </summary>
    public static void SetMcpPath(string path)
    {
        var config = Load();
        config.McpPath = path;
        config.Save();
    }

    /// <summary>
    /// Clears the configured MCP path.
    /// </summary>
    public static bool ClearMcpPath()
    {
        var config = Load();
        if (string.IsNullOrEmpty(config.McpPath))
            return false;

        config.McpPath = null;
        config.Save();
        return true;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GlobalConfig))]
internal partial class GlobalConfigJsonContext : JsonSerializerContext
{
}
