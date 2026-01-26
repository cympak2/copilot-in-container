# Use the official Node.js LTS image as base
FROM node:20-slim

# Set environment variables
ENV DEBIAN_FRONTEND=noninteractive

# Install system dependencies including gosu for user switching
RUN apt-get update && apt-get install -y \
    git \
    curl \
    ca-certificates \
    gosu \
    && rm -rf /var/lib/apt/lists/*

# ARG for the Copilot CLI version - passed from build process
ARG COPILOT_VERSION=latest

# Install the standalone GitHub Copilot CLI via npm
RUN npm install -g @github/copilot@${COPILOT_VERSION}

# Set the working directory for the container
WORKDIR /workspace

# Copy the entrypoint script and session-info into the container and make them executable
COPY entrypoint.sh /usr/local/bin/
COPY session-info.sh /usr/local/bin/session-info
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/session-info

# The entrypoint script will handle user creation and command execution
ENTRYPOINT [ "/usr/local/bin/entrypoint.sh" ]

# The default command to run if none is provided
CMD [ "copilot", "--banner" ]
