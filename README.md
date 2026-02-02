# copilot-in-container: GitHub Copilot CLI in Container

Run the GitHub Copilot CLI in a secure, sandboxed container on macOS, Linux and other platforms. This tool provides a lightweight wrapper to run GitHub Copilot CLI in isolation using Apple Container, Podman, or Docker.

## 🚀 What is this?

This project provides a simple way to run [GitHub Copilot CLI](https://github.com/features/copilot/cli) in a container, giving you:

- **Security**: Isolated environment using container technology
- **Flexibility**: Support for Apple Container, Podman, and Docker
- **Clean system**: No global Node.js installation needed
- **Automatic authentication**: Uses your existing `gh` CLI credentials
- **File system access**: Only mounts the current directory you're working in
- **Persistence**: Configuration persists across sessions

## ✅ Prerequisites

Before you start, ensure you have:

### Container Runtime (choose one)

- **Apple Container** (recommended for Apple Silicon Macs):
  - Mac with Apple silicon
  - macOS 26 or later
  - [Apple container](https://github.com/apple/container) installed
  
- **Podman** (recommended for Linux):
  - [Podman](https://podman.io/getting-started/installation) installed
  - Works on Linux, macOS, and Windows
  - Daemonless container runtime
  
- **Docker** (cross-platform):
  - [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed
  - Works on macOS (Intel or Apple Silicon), Linux, and Windows

### GitHub CLI

- The [GitHub CLI (gh)](https://cli.github.com/) installed and authenticated
- Your GitHub token must have the `copilot` and `read:packages` scopes

Check your authentication status:
```bash
gh auth status
```

If you need to add scopes:
```bash
gh auth refresh -h github.com -s copilot,read:packages
```

## 🛠️ Installation

### Quick Install

```bash
# Download and install
curl -fsSL https://raw.githubusercontent.com/yourusername/copilot-in-container/main/install.sh | bash
```

During installation, you'll be asked to choose your preferred container runtime if both are available.

### Manual Install

1. Download the script:
```bash
curl -fsSL https://raw.githubusercontent.com/yourusername/copilot-in-container/main/copilot-in-container.sh -o ~/.copilot-in-container.sh
```

2. Add to your shell profile (`~/.zshrc` or `~/.bashrc`):
```bash
source ~/.copilot-in-container.sh
```

3. Reload your shell:
```bash
source ~/.zshrc  # or source ~/.bashrc
```

4. Set your preferred runtime:
```bash
cic runtime set --runtime docker
# or
cic runtime set --runtime container
# or
cic runtime set --runtime podman
```

## 🔧 Runtime Management

### Check current runtime

```bash
cic runtime show
```

### List available runtimes

```bash
cic runtime list
```

### Switch runtime

```bash
# Switch to Docker
cic runtime set --runtime docker

# Switch to Apple Container
cic runtime set --runtime container

# Switch to Podman
cic runtime set --runtime podman
```

## 📖 Usage

### Interactive Mode

Start a full chat session:
```bash
# Basic usage
copilot-in-container
cic

# Get help
copilot-in-container --help
cic --help
```

### Non-Interactive Mode

Pass a prompt directly:
```bash
copilot-in-container "suggest a git command to view the last 5 commits"
cic "explain the code in ./my-script.js"
```

### Server Mode (Persistent Instances)

Run GitHub Copilot CLI as a persistent background server:

```bash
# Start a named server instance
cic server start --name dev

# Start with specific port
cic server start --name dev --port 3000

# List all running servers
cic server list

# Connect to a server instance
cic server connect --name dev

# Connect with a prompt
cic server connect --name dev "explain this code"

# Check server status
cic server status --name dev

# Stop a server instance
cic server stop --name dev
```

**Server Mode Benefits:**
- **Faster startup**: Server stays running, no container startup time
- **Multiple instances**: Run different servers for different projects/contexts
- **Persistent state**: Conversation context maintained across connections
- **Background operation**: Server runs independently of your shell session

### Options

- `--no-pull` - Skip pulling the latest image
- `--help` - Show help message
- `-h` - Show help message

## 🔧 How It Works

The `copilot-in-container` (or `cic`) script:

1. Checks for required dependencies (Apple container, GitHub CLI)
2. Validates your GitHub token has the necessary scopes
3. Automatically downloads the container image from GitHub Container Registry if not found locally
4. Mounts your current directory into the container
5. Passes your GitHub authentication to the container
6. Runs the GitHub Copilot CLI in an isolated Apple container

In **server mode**, the CLI runs as a persistent background process that you can connect to multiple times without restart overhead.

## 🏗️ Building the Image (Optional)

The container image is automatically downloaded from GitHub Container Registry (`ghcr.io/cympak2/copilot-in-container:latest`) when you run the CLI.

However, if you want to build the image locally (for development or customization):

```bash
container build -t ghcr.io/cympak2/copilot-in-container:latest -f Dockerfile .
```

## 🔒 Security

- The container only has access to your current working directory
- GitHub credentials are passed securely via environment variables
- Runs in isolation using Apple's container technology
- No persistent storage beyond the mounted directory

## 📝 Configuration

Configuration is stored in `~/.config/gh-copilot/` and persists across sessions.

## 🐛 Troubleshooting

### Container command not found
Make sure you have Apple container installed:
```bash
container --version
```

If not installed, download from: https://github.com/apple/container/releases

### GitHub authentication fails
Verify your authentication:
```bash
gh auth status
```

Ensure you have the required scopes:
```bash
gh auth refresh -h github.com -s copilot,read:packages
```

### Permission denied
Make sure the script is sourced in your shell profile and you've reloaded your shell.

## 📄 License

MIT License - feel free to use and modify as needed.

## 🙏 Acknowledgments

- Inspired by [copilot_here](https://github.com/GordonBeeming/copilot_here) by Gordon Beeming
- Built using [Apple container](https://github.com/apple/container)
- Powered by [GitHub Copilot CLI](https://github.com/features/copilot/cli)

## ⚠️ Platform Support

This tool **only supports macOS** with Apple silicon. It does not support:
- Intel Macs
- Linux
- Windows

For cross-platform Docker-based solution, see the original [copilot_here](https://github.com/GordonBeeming/copilot_here) project.
