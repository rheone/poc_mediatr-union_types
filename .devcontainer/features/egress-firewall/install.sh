#!/usr/bin/env bash
# Installs the firewall tooling and the scripts the Feature entrypoint runs (see devcontainer-feature.json).
# Runs at image build time as root, with this Feature's directory as the working directory.
set -euo pipefail

dest=/usr/local/share/egress-firewall

# iptables/ipset program the rules; dig (dnsutils) resolves the host list; iproute2 (ip) is used for the
# IPv6 check; curl and jq fetch and parse GitHub's published ranges.
export DEBIAN_FRONTEND=noninteractive
apt-get update
apt-get install -y --no-install-recommends ca-certificates curl jq iptables ipset dnsutils iproute2
rm -rf /var/lib/apt/lists/*

# Root-owned and not writable by anyone else: the agent cannot widen its own network access.
install -d -m 0755 "$dest"
install -m 0755 entrypoint.sh init-firewall.sh "$dest/"
install -m 0644 allowed-hosts.txt "$dest/"
