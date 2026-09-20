#!/usr/bin/env bash
# Container PID 1 (under tini). Applies the egress firewall and only then idles; if the firewall
# cannot be applied the container exits, so the dev container fails to start instead of running open.
set -euo pipefail
fw=/usr/local/share/devcontainer/init-firewall.sh
"$fw"
# Lets post-create.sh and the smoke test wait until the firewall is really in place.
touch /run/firewall.ready
# CDN addresses rotate: keep adding what the allowed hostnames currently resolve to.
while sleep 15; do "$fw" --refresh || true; done
