namespace CopilotInContainer.Infrastructure;

/// <summary>
/// Interface for container runtime implementations (Docker, Apple Container, etc.)
/// </summary>
public interface IContainerRuntime
{
    /// <summary>
    /// Gets the name of the container runtime (e.g., "docker", "container")
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Gets the display name of the container runtime
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Gets the command name used to execute container operations
    /// </summary>
    string CommandName { get; }
    
    /// <summary>
    /// Checks if the runtime is available on the system
    /// </summary>
    bool IsAvailable();
    
    /// <summary>
    /// Gets the version string of the runtime
    /// </summary>
    string GetVersion();
    
    /// <summary>
    /// Builds container run arguments for the given parameters
    /// </summary>
    List<string> BuildRunArguments(
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
    );
    
    /// <summary>
    /// Builds container exec arguments for the given parameters
    /// </summary>
    List<string> BuildExecArguments(
        string containerId,
        string workingDirectory,
        bool interactive,
        string[] command
    );
    
    /// <summary>
    /// Checks if an image exists locally
    /// </summary>
    bool CheckImageExists(string imageName);
    
    /// <summary>
    /// Pulls an image from a registry
    /// </summary>
    (int exitCode, string output) PullImage(string imageName);
    
    /// <summary>
    /// Lists running containers
    /// </summary>
    (int exitCode, string output) ListContainers();
    
    /// <summary>
    /// Gets logs from a container
    /// </summary>
    List<string> BuildLogsArguments(string containerId, int? tail = null, bool follow = false);
    
    /// <summary>
    /// Stops a container
    /// </summary>
    (int exitCode, string output) StopContainer(string containerId);
    
    /// <summary>
    /// Checks if a container is running
    /// </summary>
    bool IsContainerRunning(string containerId);
}
