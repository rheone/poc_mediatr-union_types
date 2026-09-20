#!/usr/bin/env bash
# Container entrypoint (from this Feature's metadata; the CLI runs it inside a wrapper shell under tini).
# Applies the egress firewall and only then idles; if the firewall cannot be applied the container stops,
# so the dev container fails to start instead of running open.
set -uo pipefail
fw=/usr/local/share/egress-firewall/init-firewall.sh
if ! "$fw"; then
  echo "entrypoint: firewall not applied, stopping the container" >&2
  # The wrapper shell would go on to `exec` the image's default command once this script returns; killing
  # it first makes tini (PID 1) exit, so nothing runs without the firewall.
  kill -KILL "$PPID" 2>/dev/null || true
  exit 1
fi
# Lets post-create.sh and the smoke test wait until the firewall is really in place.
touch /run/firewall.ready
# CDN addresses rotate: keep adding what the allowed hostnames currently resolve to.
while sleep 15; do "$fw" --refresh || true; done
