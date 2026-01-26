namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Generic utilities for reading/writing config files.
/// </summary>
public static class ConfigFile
{
    /// <summary>
    /// Reads a single-value config file.
    /// Returns null if file doesn't exist or is empty.
    /// </summary>
    public static string? ReadValue(string path)
    {
        if (!File.Exists(path))
            return null;

        var content = File.ReadAllText(path).Trim();
        return string.IsNullOrEmpty(content) ? null : content;
    }

    /// <summary>
    /// Writes a single value to a config file, creating directories as needed.
    /// </summary>
    public static void WriteValue(string path, string value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, value);
    }

    /// <summary>
    /// Deletes a config file if it exists.
    /// </summary>
    public static bool Delete(string path)
    {
        if (!File.Exists(path))
            return false;
        
        File.Delete(path);
        return true;
    }
}
