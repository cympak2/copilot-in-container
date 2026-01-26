# Quick Start Guide

This guide will help you get started with `copilot-in-container` (or `cic`) in just a few minutes.

## Step 1: Prerequisites

Ensure you have:
- macOS 15+ with Apple Silicon
- Apple container installed
- GitHub CLI installed

### Install Apple Container

If you're upgrading, first stop and uninstall your existing `container` (the `-k` flag keeps your user data, while `-d` removes it):

```bash
container system stop
/usr/local/bin/uninstall-container.sh -k
```

Download the latest signed installer package for `container` from the [GitHub release page](https://github.com/apple/container/releases).

To install the tool, double-click the package file and follow the instructions. Enter your administrator password when prompted, to give the installer permission to place the installed files under `/usr/local`.

Or jist use:
```bash
brew install container
```

Start the system service with:

```bash
container system start
```

For more details look at [Official Container](https://github.com/apple/container) repository

### Install GitHub CLI

```bash
brew install gh

# Authenticate
gh auth login

# Add required scopes
gh auth refresh -h github.com -s copilot,read:packages

# Verify
gh auth status
```

## Step 2: Install copilot-in-container

### Option A: Quick Install (Recommended)

```bash
# From project directory
./install.sh
```

### Option B: Manual Install

```bash
# Copy the script
cp copilot-in-container.sh ~/.copilot-in-container.sh
chmod +x ~/.copilot-in-container.sh

# Add to your shell profile
echo 'source ~/.copilot-in-container.sh' >> ~/.zshrc

# Reload shell
source ~/.zshrc
```

## Step 3: Build the Container Image

```bash
# Use the build script
./build.sh

# Or manually
container build -t gccli:latest .
```

This will:
- Download Node.js base image
- Install GitHub Copilot CLI
- Set up the container environment

## Step 4: Start Using copilot-in-container

### Interactive Mode

```bash
copilot-in-container
cic
```

This starts a full interactive session with GitHub Copilot CLI.

### Direct Prompts

```bash
# Ask a question
copilot-in-container "explain what this command does: ls -la"
cic "explain what this command does: ls -la"

# Get suggestions
cic "suggest a git command to undo the last commit"

# Explain code
cic "explain the code in ./main.py"
```

## Common Use Cases

### 1. Code Explanation

```bash
cic "explain the Dockerfile in this directory"
```

### 2. Git Commands

```bash
cic "how do I create a new branch and push it?"
```

### 3. Shell Commands

```bash
cic "find all .js files modified in the last 24 hours"
```

### 4. Debugging

```bash
cic "why might I get a 'permission denied' error?"
```

## Server Mode (Advanced)

For better performance, run Copilot CLI as a persistent background server:

### Start a Server Instance

```bash
# Start the default server
cic server start

# Start a named server for a specific project
cic server start --name myproject

# Start with a specific port
cic server start --name myproject --port 3000
```

### Connect to a Server

```bash
# Connect to default server
cic server connect

# Connect to named server
cic server connect --name myproject

# Send a prompt directly
cic server connect --name myproject "explain this code"
```

### Manage Servers

```bash
# List all running servers
cic server list

# Check status of a specific server
cic server status --name myproject

# Stop a server
cic server stop --name myproject
```

### Why Use Server Mode?

- ⚡ **Faster**: No container startup time on each use
- 🔄 **Persistent**: Conversation context maintained between connections
- 🎯 **Multiple projects**: Run separate servers for different codebases
- 🌐 **Background**: Server runs independently of your shell

## Options

- `--no-pull` - Skip pulling the latest image (faster startup)
- `--help` - Show help message

## Troubleshooting

### Container command not found

```bash
# Check if installed
container --version

# If not, install from:
# https://github.com/apple/container/releases
```

### GitHub authentication failed

```bash
# Re-authenticate
gh auth login

# Add scopes
gh auth refresh -h github.com -s copilot,read:packages

# Verify
gh auth status
```

### Image build fails

```bash
# Check Apple container is running
container system start

# Try building again
./build.sh
```

### Script not found after installation

```bash
# Reload your shell
source ~/.zshrc  # or source ~/.bashrc

# Or restart your terminal
```

## Tips

1. **Skip image pull for faster startup**: Use `--no-pull` when you know you have the latest image
2. **Interactive vs Direct**: Use interactive mode for conversations, direct prompts for quick answers
3. **Current directory**: The container only sees your current working directory - cd to the right place first
4. **Configuration persists**: Your Copilot settings are saved in `~/.config/gh-copilot/`

## Next Steps

- Read the full [README.md](README.md) for more details
- Visit [GitHub Copilot CLI docs](https://docs.github.com/en/copilot/github-copilot-in-the-cli)
- Explore [Apple container documentation](https://apple.github.io/container/documentation/)

## Support

Having issues? Check:
- Apple container is properly installed and system service is running
- GitHub CLI is authenticated with the right scopes
- You're running on macOS 15+ with Apple Silicon

For more help, see the [Troubleshooting](#troubleshooting) section in README.md.
