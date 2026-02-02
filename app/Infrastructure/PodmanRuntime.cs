using System.Diagnostics;

namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Implementation for Podman runtime (common on Linux systems)
/// </summary>
public class PodmanRuntime : IContainerRuntime
{
    public string Name => "podman";
    public string DisplayName => "Podman";
    public string CommandName => "podman";

    public bool IsAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "podman",
                ArgumentList = { "--version" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;
            
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public string GetVersion()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "podman",
                ArgumentList = { "--version" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return "unknown";
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            return process.ExitCode == 0 ? output.Trim() : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    public List<string> BuildRunArguments(
        string imageName,
        string containerName,
        Dictionary<string, string> environmentVariables,
        Dictionary<string, string> volumes,
        string workingDirectory,
        bool interactive = true,
        bool removeOnExit = true,
        Dictionary<string, string>? ports = null,
        bool detached = false,
        string? platform = null
    )
    {
        var args = new List<string> { "run" };

        // Container name
        args.Add("--name");
        args.Add(containerName);

        // Remove on exit
        if (removeOnExit)
        {
            args.Add("--rm");
        }

        // Interactive
        if (interactive)
        {
            args.Add("-it");
        }

        // Detached mode
        if (detached)
        {
            args.Add("-d");
        }

        // Platform
        if (!string.IsNullOrEmpty(platform))
        {
            args.Add("--platform");
            args.Add(platform);
        }

        // Environment variables
        foreach (var (key, value) in environmentVariables)
        {
            args.Add("-e");
            args.Add($"{key}={value}");
        }

        // Volumes
        foreach (var (hostPath, containerPath) in volumes)
        {
            args.Add("-v");
            args.Add($"{hostPath}:{containerPath}");
        }

        // Working directory
        if (!string.IsNullOrEmpty(workingDirectory))
        {
            args.Add("-w");
            args.Add(workingDirectory);
        }

        // Port mappings
        if (ports != null)
        {
            foreach (var (hostPort, containerPort) in ports)
            {
                args.Add("-p");
                args.Add($"{hostPort}:{containerPort}");
            }
        }

        // Image name (must be last before command)
        args.Add(imageName);

        return args;
    }

    public List<string> BuildExecArguments(
        string containerId,
        string workingDirectory,
        bool interactive,
        string[] command)
    {
        var args = new List<string> { "exec" };

        if (interactive)
            args.Add("-it");

        args.Add("-w");
        args.Add(workingDirectory);
        
        args.Add(containerId);
        args.AddRange(command);

        return args;
    }

    public bool CheckImageExists(string imageName)
    {
        var (exitCode, output) = RunCommand("images", "--format", "{{.Repository}}:{{.Tag}}");
        
        if (exitCode != 0) return false;

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        // Normalize image name
        var normalizedImageName = imageName.Contains(':') ? imageName : $"{imageName}:latest";
        
        foreach (var line in lines)
        {
            if (line.Trim() == normalizedImageName)
                return true;
        }

        return false;
    }

    public (int exitCode, string output) PullImage(string imageName)
    {
        return RunCommand("pull", imageName);
    }

    public (int exitCode, string output) ListContainers()
    {
        return RunCommand("ps", "-a");
    }

    public List<string> BuildLogsArguments(string containerId, int? tail = null, bool follow = false)
    {
        var args = new List<string> { "logs" };
        
        if (tail.HasValue)
        {
            args.Add("--tail");
            args.Add(tail.Value.ToString());
        }
        
        if (follow)
            args.Add("--follow");
        
        args.Add(containerId);
        
        return args;
    }

    public (int exitCode, string output) StopContainer(string containerId)
    {
        return RunCommand("stop", containerId);
    }

    public bool IsContainerRunning(string containerId)
    {
        var (exitCode, output) = RunCommand("ps", "--filter", $"id={containerId}", "--format", "{{.State}}");
        
        if (exitCode != 0) return false;
        
        return output.Trim().Equals("running", StringComparison.OrdinalIgnoreCase);
    }

    private (int exitCode, string output) RunCommand(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "podman",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
                startInfo.ArgumentList.Add(arg);

            using var process = Process.Start(startInfo);
            if (process == null)
                return (-1, string.Empty);

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output.Trim());
        }
        catch
        {
            return (-1, string.Empty);
        }
    }
}
