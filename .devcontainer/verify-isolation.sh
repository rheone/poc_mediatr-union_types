#!/usr/bin/env bash
# Asserts, from inside the running dev container, the negatives that make it host-isolated.
# Exits non-zero if any check fails. Run it after every rebuild and whenever the config changes.
#   verify-isolation.sh            all checks
#   verify-isolation.sh --offline  skip the network checks (fast local iteration only)
# The checks run in child shells (`bash -c '...'`) on purpose, so single-quoted expansions (SC2016) are
# expected, and the network helpers are invoked from those children (SC2317).
# shellcheck disable=SC2016,SC2317
set -uo pipefail

offline=0
[ "${1:-}" = --offline ] && offline=1
if [ "$offline" = 0 ]; then
  # The firewall is applied by PID 1 in the background of container start; wait for it (max 2 min).
  for _ in $(seq 1 120); do [ -e /run/firewall.ready ] && break; sleep 1; done
fi
fails=0
pass() { printf 'PASS  %s\n' "$1"; }
fail() { printf 'FAIL  %s\n' "$1"; fails=$((fails + 1)); }
check() { # check "<description>" <command...>  (passes when the command succeeds)
  local desc="$1"
  shift
  if "$@" >/dev/null 2>&1; then pass "$desc"; else fail "$desc"; fi
}
status_field() { awk -v k="$1:" '$1 == k {print $2}' /proc/self/status; }

# ---- identity and privileges ------------------------------------------------------------------
check "running as a non-root user (uid $(id -u))" test "$(id -u)" -ne 0
check "no sudo installed" bash -c '! command -v sudo && ! command -v su-exec'
check "no setuid/setgid binaries in the image" bash -c '[ -z "$(find /usr /bin /sbin /opt -xdev -type f -perm /6000 -print -quit 2>/dev/null)" ]'
check "no_new_privs is set" test "$(status_field NoNewPrivs)" = 1
check "seccomp filter is active" test "$(status_field Seccomp)" = 2
check "effective capabilities are empty" test "$(status_field CapEff)" = 0000000000000000
# Bounding set may hold only NET_ADMIN (12) and NET_RAW (13) = 0x3000, which PID 1 needs for the firewall.
bnd="$(status_field CapBnd)"
check "capability bounding set is at most NET_ADMIN+NET_RAW (CapBnd=$bnd)" test $(( 0x${bnd:-ffffffffffffffff} & ~0x3000 )) -eq 0
check "not able to change firewall rules (iptables)" bash -c '! iptables -P OUTPUT ACCEPT'
check "image tools are read-only for this user" bash -c '! test -w /opt/npm-global && ! test -w /usr/local/share/devcontainer/allowed-hosts.txt'

# ---- host filesystem, sockets, devices --------------------------------------------------------
# Host-backed filesystem types on Docker Desktop: 9p/drvfs (Windows), virtiofs/fuse (macOS), plus network shares.
host_mounts="$(awk '{ for (i = 1; i <= NF; i++) if ($i == "-") { t = $(i + 1); break }
                       if (t ~ /^(9p|drvfs|virtiofs|fuse.*|cifs|smb3|nfs.*|vboxsf|fakeowner)$/) print $5 " (" t ")" }' /proc/self/mountinfo)"
if [ -z "$host_mounts" ]; then pass "no host-backed filesystems mounted (no 9p/virtiofs/fuse/cifs/nfs)"; else fail "host-backed mounts present: $host_mounts"; fi
check "no /mnt/c, /host_mnt or /run/desktop host paths" bash -c '! ls -d /mnt/[a-z] /host_mnt /run/desktop /Users /c 2>/dev/null | head -n1 | read -r _'
check "Docker socket absent" bash -c '! test -S /var/run/docker.sock && ! test -S /run/docker.sock && [ -z "${DOCKER_HOST:-}" ] && ! command -v docker'
check "no GPU, USB, KVM, FUSE, audio or video devices" bash -c '! ls -d /dev/dri /dev/nvidia* /dev/bus/usb /dev/kvm /dev/fuse /dev/snd /dev/video* /dev/sd* /dev/vd* /dev/nvme* 2>/dev/null | head -n1 | read -r _'
check "no display/clipboard sockets (X11, Wayland)" bash -c '[ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ] && ! test -e /tmp/.X11-unix && ! ls "${XDG_RUNTIME_DIR:-/nonexistent}"/wayland-* 2>/dev/null | head -n1 | read -r _'
check "HOME is the container user's own ($HOME)" test "$HOME" = /home/vscode

# The workspace must be a Docker volume, and Claude's config its own volume (not the image layer, not a host path).
ws="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
wsfs="$(stat -f -c %T "$ws" 2>/dev/null)"
case "$wsfs" in
  v9fs | virtiofs | fuseblk | fuse* | cifs | smb* | nfs*) fail "workspace $ws is on a host filesystem ($wsfs): use Clone Repository in Container Volume" ;;
  *) pass "workspace $ws is not host-backed ($wsfs)" ;;
esac
check "HOME/.claude is its own mount (named volume)" bash -c 'awk -v p="$HOME/.claude" "\$5 == p {f=1} END {exit !f}" /proc/self/mountinfo'
check "NuGet cache is its own mount (named volume)" bash -c 'awk -v p="$HOME/.nuget/packages" "\$5 == p {f=1} END {exit !f}" /proc/self/mountinfo'

# ---- credentials must not flow in implicitly --------------------------------------------------
check "no SSH agent forwarded" bash -c '[ -z "${SSH_AUTH_SOCK:-}" ] && ! ls /tmp/vscode-ssh-auth* 2>/dev/null | head -n1 | read -r _'
check "no host git credential helper injected" bash -c '[ -z "$(git config --get-all credential.helper)" ]'
check "no host key/cloud credential directories (~/.ssh ~/.aws ~/.azure ~/.kube ~/.docker/config.json)" \
  bash -c 'for p in .ssh .aws .azure .kube .docker/config.json .config/gcloud; do [ ! -e "$HOME/$p" ] || exit 1; done'
check "no token variables in the environment" bash -c '! env | rg -q "^(ANTHROPIC_API_KEY|CLAUDE_CODE_OAUTH_TOKEN|GH_TOKEN|GITHUB_TOKEN|AWS_[A-Z_]*KEY[A-Z_]*|AZURE_CLIENT_SECRET|NUGET_API_KEY)="'

# ---- network: default deny, allow-list only ---------------------------------------------------
if [ "$offline" = 0 ]; then
  reach() { # reach <url>: any HTTP status counts as reachable; 000 means blocked/unreachable
    local code
    code="$(curl -sS -o /dev/null --connect-timeout 5 --max-time 10 -w '%{http_code}' "$@" 2>/dev/null)" || true
    [ -n "$code" ] && [ "$code" != 000 ]
  }
  # CDN-backed hosts rotate addresses; the firewall re-resolves every 15s, so an allowed host may be
  # rejected once right after a rotation. Allowed checks therefore retry; blocked checks never do.
  reach_retry() { for _ in 1 2 3; do reach "$@" && return 0; sleep 8; done; return 1; }
  check "egress BLOCKED: https://example.com" bash -c "$(declare -f reach); ! reach https://example.com"
  check "egress BLOCKED: https://1.1.1.1 (raw IP)" bash -c "$(declare -f reach); ! reach https://1.1.1.1"
  check "egress BLOCKED over IPv6: example.com" bash -c "$(declare -f reach); ! reach -6 https://example.com"
  gw="$(ip route 2>/dev/null | awk '/^default/ {print $3; exit}')"
  if [ -n "$gw" ]; then
    check "host/gateway $gw not reachable on 22, 80, 445, 2375" bash -c "for p in 22 80 445 2375; do timeout 3 bash -c \"echo > /dev/tcp/$gw/\$p\" 2>/dev/null && exit 1; done; exit 0"
  fi
  check "egress ALLOWED: https://api.github.com" bash -c "$(declare -f reach reach_retry); reach_retry https://api.github.com/zen"
  check "egress ALLOWED: https://api.anthropic.com" bash -c "$(declare -f reach reach_retry); reach_retry https://api.anthropic.com"
  check "egress ALLOWED: https://api.nuget.org" bash -c "$(declare -f reach reach_retry); reach_retry https://api.nuget.org/v3/index.json"
else
  echo "SKIP  network checks (--offline)"
fi

echo
if [ "$fails" -eq 0 ]; then echo "verify-isolation: all checks passed"; else echo "verify-isolation: $fails check(s) FAILED"; fi
exit $((fails > 0))
