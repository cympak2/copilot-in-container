namespace CopilotInContainer.Infrastructure;

internal static class AgentDiscovery
{
    private const string AgentsDirectory = ".github/agents";

    public static IReadOnlyList<DiscoveredAgent> FindAgentsInWorkspace(string workspaceFolder)
    {
        if (string.IsNullOrWhiteSpace(workspaceFolder))
        {
            return [];
        }

        var agentsPath = Path.Combine(workspaceFolder, AgentsDirectory);
        if (!Directory.Exists(agentsPath))
        {
            return [];
        }

        return Directory.GetFiles(agentsPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedAgentFile)
            .OrderBy(Path.GetFileName)
            .Select(agentFile => new DiscoveredAgent(
                Name: Path.GetFileNameWithoutExtension(agentFile),
                FileName: Path.GetFileName(agentFile),
                FileSizeBytes: new FileInfo(agentFile).Length,
                Description: TryReadDescription(agentFile)))
            .ToList();
    }

    public static string GetAgentsDirectoryPath(string workspaceFolder) =>
        Path.Combine(workspaceFolder, AgentsDirectory);

    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private static bool IsSupportedAgentFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".md" or ".yml" or ".yaml" or ".json";
    }

    private static string? TryReadDescription(string agentFile)
    {
        try
        {
            var firstLine = File.ReadLines(agentFile).FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(firstLine))
            {
                return null;
            }

            if (firstLine.StartsWith("#"))
            {
                firstLine = firstLine.TrimStart('#').Trim();
            }

            if (firstLine.Length > 60)
            {
                firstLine = firstLine.Substring(0, 57) + "...";
            }

            return firstLine;
        }
        catch
        {
            return null;
        }
    }
}

internal record DiscoveredAgent(
    string Name,
    string FileName,
    long FileSizeBytes,
    string? Description);