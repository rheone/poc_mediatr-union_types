#!/usr/bin/env bash
# Runs last, at image build time as root. Nothing here adds tools; it only removes ways to gain privileges
# and fixes ownership. See docs/DevContainer.md, "Isolation model".
set -euo pipefail

user="${_REMOTE_USER:?_REMOTE_USER is not set: set remoteUser in devcontainer.json}"
home="${_REMOTE_USER_HOME:?}"
export DEBIAN_FRONTEND=noninteractive

# The base image ships sudo (passwordless for the vscode user) and an ssh client. The agent needs neither:
# git uses HTTPS and the firewall drops port 22. SUDO_FORCE_REMOVE: dpkg refuses to remove sudo while root has
# no password, which is exactly the intended end state.
SUDO_FORCE_REMOVE=yes apt-get purge -y sudo openssh-client
rm -rf /etc/sudoers /etc/sudoers.d /var/lib/apt/lists/*

# The node Feature installs nvm and node owned by the user, which would let the agent replace node.
# Hand them to root; the user only needs to read and execute.
nvm_dir="${NVM_DIR:-/usr/local/share/nvm}"
if [ -d "$nvm_dir" ]; then
  chown -R root:root "$nvm_dir"
  chmod -R go-w "$nvm_dir"
fi

# Named-volume mount points must exist with the right owner: Docker copies that ownership into a new
# named volume on first use, so the non-root user can write to it (devcontainer.json "mounts").
install -d -o "$user" -g "$user" "$home/.claude" "$home/.nuget" "$home/.nuget/packages" "$home/.cache"

# Strip every setuid and setgid bit so nothing in the image can raise privileges.
find / -xdev -type f \( -perm -4000 -o -perm -2000 \) -exec chmod a-s {} + 2>/dev/null || true
