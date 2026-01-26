#!/bin/bash
set -e

# Get the user and group IDs from environment variables, default to 1000 if not set.
USER_ID=${PUID:-1000}
GROUP_ID=${PGID:-1000}

# If the desired IDs are already in use in the base image (e.g. node:1000),
# move those accounts out of the way so we can run as appuser with the host UID/GID.
existing_group=$(getent group "$GROUP_ID" | cut -d: -f1 || true)
if [ -n "$existing_group" ] && [ "$existing_group" != "appuser_group" ]; then
  NEW_GROUP_ID=$((GROUP_ID + 1))
  while getent group "$NEW_GROUP_ID" >/dev/null 2>&1; do
    NEW_GROUP_ID=$((NEW_GROUP_ID + 1))
  done
  groupmod -g "$NEW_GROUP_ID" "$existing_group" >/dev/null 2>&1 || true
fi

existing_user=$(getent passwd "$USER_ID" | cut -d: -f1 || true)
if [ -n "$existing_user" ] && [ "$existing_user" != "appuser" ]; then
  NEW_USER_ID=$((USER_ID + 1))
  while id -u "$NEW_USER_ID" >/dev/null 2>&1; do
    NEW_USER_ID=$((NEW_USER_ID + 1))
  done
  usermod -u "$NEW_USER_ID" "$existing_user" >/dev/null 2>&1 || true

  user_gid=$(id -g "$existing_user" 2>/dev/null || true)
  user_home=$(getent passwd "$existing_user" | cut -d: -f6 || true)
  if [ -n "$user_home" ] && [ -d "$user_home" ]; then
    if [ -n "$user_gid" ]; then
      chown -R "$NEW_USER_ID:$user_gid" "$user_home" >/dev/null 2>&1 || true
    else
      chown -R "$NEW_USER_ID" "$user_home" >/dev/null 2>&1 || true
    fi
  fi
fi

# Create a group and user with the requested IDs.
groupadd --gid "$GROUP_ID" appuser_group >/dev/null 2>&1 || true
useradd --uid "$USER_ID" --gid "$GROUP_ID" --shell /bin/bash --create-home appuser >/dev/null 2>&1 || true

# Verify the user was created successfully
if ! id appuser >/dev/null 2>&1; then
  echo "Warning: Failed to create appuser, running as root" >&2
  mkdir -p /home/appuser/.copilot
  exec "$@"
fi

# Set up directories with correct ownership (avoid chowning /home/appuser wholesale,
# because /home/appuser/** can include bind mounts to the host).
mkdir -p /home/appuser
mkdir -p /home/appuser/.copilot
mkdir -p /home/appuser/.dotnet
mkdir -p /home/appuser/.nuget
mkdir -p /home/appuser/.local
mkdir -p /home/appuser/.cache
mkdir -p /home/appuser/.config
mkdir -p /home/appuser/.npm

# Try to chown directories, but don't fail if they're bind mounts
chown -R "$USER_ID:$GROUP_ID" /home/appuser/.copilot 2>/dev/null || true
chown -R "$USER_ID:$GROUP_ID" /home/appuser/.dotnet 2>/dev/null || true
chown -R "$USER_ID:$GROUP_ID" /home/appuser/.nuget 2>/dev/null || true
chown -R "$USER_ID:$GROUP_ID" /home/appuser/.local 2>/dev/null || true
chown -R "$USER_ID:$GROUP_ID" /home/appuser/.cache 2>/dev/null || true
chown -R "$USER_ID:$GROUP_ID" /home/appuser/.config 2>/dev/null || true
chown -R "$USER_ID:$GROUP_ID" /home/appuser/.npm 2>/dev/null || true

export HOME=/home/appuser

# Switch to the user matching the host UID and execute the command passed to the script.
# Note: Apple container doesn't have gosu, so we use su-exec or su
if command -v gosu >/dev/null 2>&1; then
  exec gosu appuser "$@"
elif command -v su-exec >/dev/null 2>&1; then
  exec su-exec appuser "$@"
else
  exec su -s /bin/bash appuser -c "cd $PWD && exec \"\$@\"" -- "$@"
fi
