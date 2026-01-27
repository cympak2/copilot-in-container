# Example App - Copilot SDK Client

This is an example .NET application that demonstrates how to connect to a GitHub Copilot CLI server using the Copilot SDK.

## Usage

### Option 1: Connect to a running cic server

First, start a server using cic:

```bash
cd copilot-in-container
./cic server start --name myserver
./cic server list  # Note the port number
```

Then run the example app with the server URL:

```bash
cd example_app
dotnet run localhost:35137 "your prompt here"
```

Replace `35137` with the actual port from `cic server list`.

### Option 2: Local mode (starts its own Copilot CLI)

Run without arguments to start a new Copilot CLI instance:

```bash
dotnet run
```

Or with a custom prompt:

```bash
dotnet run "" "What is the capital of France?"
```

## Examples

```bash
# Connect to server on port 35137 with default prompt
dotnet run localhost:35137

# Connect to server with custom prompt
dotnet run localhost:35137 "Explain async/await in C#"

# Local mode with custom prompt
dotnet run "" "Write a hello world in Python"
```

## How it works

The app uses the GitHub Copilot SDK for .NET:

- If you provide a server URL (e.g., `localhost:35137`), it connects to that running server
- If you don't provide a URL, it starts its own Copilot CLI process
- The second argument onwards is used as the prompt

This demonstrates how you can:
1. Start persistent Copilot servers with `cic server start`
2. Connect multiple applications to the same server
3. Share context and session state across different clients
