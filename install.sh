#!/bin/bash
# Installation script for copilot-in-container
# Downloads and sets up the GitHub Copilot CLI wrapper for Apple container

set -e

# Colors
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m'

print_error() {
    echo -e "${RED}❌ $1${NC}" >&2
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

print_header() {
    echo ""
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""
}

# Detect shell
detect_shell() {
    if [ -n "$ZSH_VERSION" ]; then
        echo "zsh"
    elif [ -n "$BASH_VERSION" ]; then
        echo "bash"
    else
        echo "unknown"
    fi
}

# Get shell RC file
get_shell_rc() {
    local shell_type="$1"
    
    case "$shell_type" in
        zsh)
            echo "$HOME/.zshrc"
            ;;
        bash)
            if [ -f "$HOME/.bashrc" ]; then
                echo "$HOME/.bashrc"
            else
                echo "$HOME/.bash_profile"
            fi
            ;;
        *)
            echo ""
            ;;
    esac
}

# Check if running on macOS with Apple Silicon
check_platform() {
    print_header "Checking Platform"
    
    if [ "$(uname)" != "Darwin" ]; then
        print_error "This tool only works on macOS"
        echo ""
        echo "For Docker-based cross-platform solution, see:"
        echo "  https://github.com/GordonBeeming/copilot_here"
        exit 1
    fi
    
    local arch
    arch=$(uname -m)
    if [ "$arch" != "arm64" ]; then
        print_error "This tool requires Apple Silicon (arm64)"
        echo ""
        echo "Your architecture: $arch"
        echo ""
        echo "For Intel Macs, use the Docker-based solution:"
        echo "  https://github.com/GordonBeeming/copilot_here"
        exit 1
    fi
    
    print_success "Platform: macOS $(sw_vers -productVersion) on Apple Silicon"
}

# Download the script
download_script() {
    print_header "Downloading copilot-in-container.sh"
    
    local script_path="$HOME/.copilot-in-container.sh"
    local script_url="https://raw.githubusercontent.com/cympak2/copilot-in-container/main/copilot-in-container.sh"
    
    # For local testing, you might want to use a local file
    if [ -f "$(dirname "$0")/copilot-in-container.sh" ]; then
        print_info "Using local copilot-in-container.sh for installation"
        cp "$(dirname "$0")/copilot-in-container.sh" "$script_path"
    else
        print_info "Downloading from GitHub..."
        if command -v curl >/dev/null 2>&1; then
            curl -fsSL "$script_url" -o "$script_path"
        elif command -v wget >/dev/null 2>&1; then
            wget -q "$script_url" -O "$script_path"
        else
            print_error "Neither curl nor wget found. Please install one of them."
            exit 1
        fi
    fi
    
    chmod +x "$script_path"
    print_success "Downloaded to: $script_path"
}

# Update shell configuration
update_shell_config() {
    print_header "Updating Shell Configuration"
    
    local shell_type
    shell_type=$(detect_shell)
    
    if [ "$shell_type" = "unknown" ]; then
        print_error "Could not detect shell type"
        echo ""
        echo "Please manually add the following to your shell RC file:"
        echo "  source ~/.copilot-in-container.sh"
        return 1
    fi
    
    local rc_file
    rc_file=$(get_shell_rc "$shell_type")
    
    if [ -z "$rc_file" ]; then
        print_error "Could not determine shell RC file"
        return 1
    fi
    
    # Check if already sourced
    if grep -q "\.copilot-in-container\.sh" "$rc_file" 2>/dev/null; then
        print_info "Already configured in $rc_file"
        return 0
    fi
    
    # Add source line
    echo "" >> "$rc_file"
    echo "# GitHub Copilot CLI in Apple Container" >> "$rc_file"
    echo "source ~/.copilot-in-container.sh" >> "$rc_file"
    
    print_success "Added to $rc_file"
    echo ""
    print_info "Run: source $rc_file"
    print_info "Or restart your terminal to use copilot-in-container (cic)"
}

# Check prerequisites
check_prerequisites() {
    print_header "Checking Prerequisites"
    
    local all_ok=true
    local has_container=false
    local has_docker=false
    
    # Check for Apple container
    if command -v container >/dev/null 2>&1; then
        local version
        version=$(container --version 2>&1 | head -n1 || echo "unknown")
        print_success "Apple container: $version"
        has_container=true
    fi
    
    # Check for Docker
    if command -v docker >/dev/null 2>&1; then
        local docker_version
        docker_version=$(docker --version 2>&1 || echo "unknown")
        print_success "Docker: $docker_version"
        has_docker=true
    fi
    
    # Ensure at least one container runtime is available
    if [ "$has_container" = false ] && [ "$has_docker" = false ]; then
        print_error "No container runtime found"
        echo ""
        echo "Install one of the following:"
        echo "  - Apple container: https://github.com/apple/container/releases (macOS 26+)"
        echo "  - Docker: https://docs.docker.com/get-docker/"
        all_ok=false
    fi
    
    # Check for GitHub CLI
    if command -v gh >/dev/null 2>&1; then
        local gh_version
        gh_version=$(gh --version 2>&1 | head -n1 || echo "unknown")
        print_success "GitHub CLI: $gh_version"
    else
        print_error "GitHub CLI not found"
        echo ""
        echo "Install with: brew install gh"
        echo "Or visit: https://cli.github.com/"
        all_ok=false
    fi
    
    if [ "$all_ok" = false ]; then
        echo ""
        print_error "Please install missing prerequisites and run again"
        exit 1
    fi
    
    # Return available runtimes for later selection
    echo "$has_container:$has_docker"
}

# Select container runtime
select_runtime() {
    local has_container="$1"
    local has_docker="$2"
    
    # If both are available, ask user
    if [ "$has_container" = true ] && [ "$has_docker" = true ]; then
        print_header "Select Container Runtime"
        echo ""
        echo "Both Apple Container and Docker are available."
        echo "Which one would you like to use?"
        echo ""
        echo "  1) Apple Container (native macOS runtime)"
        echo "  2) Docker"
        echo ""
        
        while true; do
            read -p "Enter choice [1-2]: " choice
            case $choice in
                1)
                    echo "container"
                    return
                    ;;
                2)
                    echo "docker"
                    return
                    ;;
                *)
                    print_error "Invalid choice. Please enter 1 or 2."
                    ;;
            esac
        done
    elif [ "$has_container" = true ]; then
        echo "container"
    elif [ "$has_docker" = true ]; then
        echo "docker"
    fi
}

# Save runtime configuration
save_runtime_config() {
    local runtime="$1"
    local config_dir="$HOME/.config/copilot-in-container"
    local config_file="$config_dir/runtime"
    
    mkdir -p "$config_dir"
    echo "$runtime" > "$config_file"
    
    print_success "Runtime preference saved: $runtime"
}

# Main installation
main() {
    echo ""
    echo "╔════════════════════════════════════════════════════════╗"
    echo "║                                                        ║"
    echo "║       copilot-in-container Installation               ║"
    echo "║       GitHub Copilot CLI in Container                 ║"
    echo "║                                                        ║"
    echo "╚════════════════════════════════════════════════════════╝"
    
    check_platform
    
    local runtime_info
    runtime_info=$(check_prerequisites)
    local has_container=$(echo "$runtime_info" | cut -d: -f1)
    local has_docker=$(echo "$runtime_info" | cut -d: -f2)
    
    local selected_runtime
    selected_runtime=$(select_runtime "$has_container" "$has_docker")
    
    save_runtime_config "$selected_runtime"
    
    download_script
    update_shell_config
    
    print_header "Installation Complete"
    
    local runtime_display
    if [ "$selected_runtime" = "container" ]; then
        runtime_display="Apple Container"
    else
        runtime_display="Docker"
    fi
    
    echo "Container Runtime: $runtime_display"
    echo ""
    echo "Next steps:"
    echo ""
    echo "1. Reload your shell:"
    echo "   source ~/.zshrc  # or source ~/.bashrc"
    echo ""
    echo "2. Authenticate with GitHub (if not already done):"
    echo "   gh auth login"
    echo "   gh auth refresh -h github.com -s copilot,read:packages"
    echo ""
    echo "3. Build the container image:"
    echo "   cd $(dirname "$0")"
    echo "   $selected_runtime build -t copilot-in-container:latest ."
    echo ""
    echo "4. Start using copilot-in-container:"
    echo "   copilot-in-container"
    echo "   cic"
    echo "   cic \"your prompt here\""
    echo ""
    print_success "Installation successful!"
    echo ""
}

# Run installation
main "$@"
