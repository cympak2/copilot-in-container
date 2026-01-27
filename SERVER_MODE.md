# Server Mode Guide

GitHub Copilot CLI can run in server mode, allowing you to maintain persistent instances that you can connect to multiple times without container startup overhead.

## Overview

Server mode runs GitHub Copilot CLI as a background service using the `--server` flag. This enables:

- **Faster connections**: No container startup time after the initial server start
- **Persistent conversations**: Context and session state maintained across connections
- **Multiple instances**: Run separate servers for different projects or contexts
- **Background operation**: Server runs independently of your terminal session

## Quick Start

### Start a Server

```bash
# Start a server with default name
cic server start

# Start a named server for a specific project
cic server start --name myproject

# Start with a specific TCP port
cic server start --name myproject --port 3000

# Start with custom model and log level
cic server start --name prod --model gpt-4 --log-level debug
```

### Connect to a Server

```bash
# Interactive session
cic server connect --name myproject

# Direct prompt
cic server connect --name myproject "refactor this function"
```

### Manage Servers

```bash
# List all servers
cic server list

# Check server status
cic server status --name myproject

# Stop a server
cic server stop --name myproject
```

## Architecture

### Server Process

When you start a server instance:

1. A detached container is created running `copilot --server --port <PORT>`
2. The server listens on a TCP port for JSON-RPC connections
3. Server state (container ID, port, etc.) is saved to `~/.copilot-in-container/servers/<name>.json`
4. GitHub authentication is passed via environment variable
5. Configuration persists in `~/.config/gh-copilot`

### Client Connection

When you connect to a server:

1. The client reads the server state file to get connection details
2. Uses `container exec` to run the copilot CLI client inside the running container
3. The CLI client connects to the server via TCP (localhost)
4. Interactive or prompt-based interaction proceeds normally

## Use Cases

### 1. Active Development

Keep a server running while working on a project:

```bash
# Start server in the morning
cic server start --name current-project

# Use throughout the day
cic server connect --name current-project
cic server connect --name current-project "explain this error"

# Stop when done
cic server stop --name current-project
```

### 2. Multiple Projects

Run separate servers for different codebases:

```bash
cic server start --name frontend --port 3001
cic server start --name backend --port 3002
cic server start --name docs --port 3003

# Switch between them
cic server connect --name frontend
cic server connect --name backend
```

### 3. Long-Running Sessions

Maintain conversation context across multiple interactions:

```bash
cic server start --name learning

# First interaction
cic server connect --name learning "explain how React hooks work"

# Later, continue the conversation
cic server connect --name learning "now show me an example with useEffect"

# Context is preserved!
cic server connect --name learning "can you combine that with useState?"
```

## Server State Management

Server state files are stored in `~/.copilot-in-container/servers/` with one JSON file per instance:

```json
{
  "InstanceName": "myproject",
  "ContainerId": "abc123...",
  "ContainerName": "copilot-server-myproject",
  "Port": 3000,
  "Model": "gpt-4",
  "LogLevel": "info",
  "StartedAt": "2026-01-23T10:30:00Z"
}
```

This allows the CLI to:
- Track which servers are running
- Reconnect to existing servers
- Display server status and uptime
- Clean up when servers are stopped

## Comparison: Direct Mode vs Server Mode

### Direct Mode (Default)
```bash
cic "help me with git"
```

**Pros:**
- Simple, one command
- No state management needed
- Automatic cleanup

**Cons:**
- Container startup time on every use (~2-5 seconds)
- No conversation persistence
- Cannot maintain context across calls

### Server Mode
```bash
cic server start --name dev
cic server connect --name dev "help me with git"
```

**Pros:**
- Instant connection after initial start
- Conversation context persists
- Can maintain multiple named instances
- Better for extended work sessions

**Cons:**
- Requires explicit start/stop
- Uses system resources when idle
- Need to remember to stop servers

## Best Practices

1. **Name your servers meaningfully**
   ```bash
   cic server start --name frontend-dev
   cic server start --name api-testing
   ```

2. **Stop servers when done**
   ```bash
   # End of day cleanup
   cic server list
   cic server stop --name old-project
   ```

3. **Use different servers for different contexts**
   - Don't mix different projects in one server
   - Keep learning/exploration separate from production work

4. **Check status before connecting**
   ```bash
   cic server status --name myproject
   ```

5. **Monitor running servers**
   ```bash
   cic server list  # Shows uptime and resource usage
   ```

## Technical Details

### GitHub Copilot CLI Server Parameters

The copilot CLI server is started with:

```bash
copilot --server --log-level <level> [--port <port>]
```

Parameters:
- `--server`: Enable server mode (TCP JSON-RPC)
- `--log-level`: Logging verbosity (error, warn, info, debug)
- `--port`: TCP port to listen on (auto-assigned if omitted)
- `--stdio`: Not used in server mode (server uses TCP)

### Container Runtime

Servers run as detached containers:

```bash
container run -d \
  --name copilot-server-<instance> \
  -e GITHUB_TOKEN=... \
  -v ~/.config/gh-copilot:/home/appuser/.copilot:rw \
  copilot-in-container:latest \
  copilot --server --log-level info --port 3000
```

### Connection Method

Clients connect via container exec:

```bash
container exec -it copilot-server-<instance> \
  copilot [prompt]
```

The copilot CLI client automatically detects and connects to the server running on localhost.

## Troubleshooting

### Server won't start

**Check if name is already in use:**
```bash
cic server list
cic server stop --name conflicting-name
```

**Verify container image:**
```bash
container image ls | grep copilot-in-container
./build.sh  # If image missing
```

### Can't connect to server

**Check if server is running:**
```bash
cic server status --name myserver
```

**Restart the server:**
```bash
cic server stop --name myserver
cic server start --name myserver
```

### Server consumes too many resources

**Check uptime:**
```bash
cic server list
```

**Stop idle servers:**
```bash
cic server stop --name old-server
```

### Lost server state

If state files are corrupted or deleted:

```bash
# Find orphaned containers
container ps -a | grep copilot-server

# Clean up manually
container stop copilot-server-<name>
container rm copilot-server-<name>

# Start fresh
cic server start --name <name>
```

## Advanced Configuration

### Custom Log Levels

```bash
# Debug mode for troubleshooting
cic server start --name debug --log-level debug

# Quiet mode for production
cic server start --name prod --log-level error
```

### Port Management

```bash
# Explicit port assignment
cic server start --name web --port 3000
cic server start --name api --port 3001

# Auto-assigned ports
cic server start --name auto  # Port chosen automatically
```

### Model Selection

```bash
# Start with specific model
cic server start --name gpt4 --model gpt-4
cic server start --name claude --model claude-3-opus
```

## Future Enhancements

Potential improvements for server mode:

- [ ] Auto-restart on container host reboot
- [ ] Health monitoring and auto-recovery
- [ ] Resource usage metrics (CPU, memory)
- [ ] Server groups (start/stop multiple servers)
- [ ] Session export/import
- [ ] Remote server support (connect to servers on other hosts)
- [ ] WebSocket support for web-based clients

## See Also

- [GitHub Copilot SDK Documentation](https://github.com/github/copilot-sdk)
- [README.md](README.md) - Main project documentation
- [QUICKSTART.md](QUICKSTART.md) - Quick start guide
