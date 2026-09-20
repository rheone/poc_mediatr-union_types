#!/usr/bin/env bash
# Default-deny egress firewall. Adapted from Anthropic's reference dev container
# (anthropics/claude-code/.devcontainer/init-firewall.sh), with these differences:
#   - runs from the Feature entrypoint (PID 1 chain) as root, so the user needs no sudo;
#   - the host/gateway network is NOT allowed (the reference allows the whole /24 of the host);
#   - no SSH (git uses HTTPS only), and DNS only to private-range resolvers;
#   - IPv6 is dropped rather than ignored; the host list lives in allowed-hosts.txt;
#   - any failure exits non-zero and leaves the default policies at DROP (fail closed).
# Usage: init-firewall.sh            apply the firewall (fails closed)
#        init-firewall.sh --refresh  re-resolve the host list and add new addresses (add-only)
# Best-effort control, not a security boundary: see docs/DevContainer.md.
set -euo pipefail
IFS=$'\n\t'

HOSTS_FILE="${HOSTS_FILE:-/usr/local/share/egress-firewall/allowed-hosts.txt}"
SET=allowed-v4

is_ipv4() { [[ "$1" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}(/[0-9]{1,2})?$ ]]; }

# Resolve every allowed host into the ipset. CDN-backed hosts (api.nuget.org is on Azure Front Door)
# rotate through several addresses, so this is repeated, and the entrypoint calls --refresh
# periodically to add newly returned ones.
resolve_hosts() {
  local strict="$1" rounds="$2" host ip found
  while read -r host; do
    host="${host%%#*}"
    host="${host//[[:space:]]/}"
    [ -z "$host" ] && continue
    found=0
    for _ in $(seq 1 "$rounds"); do
      while read -r ip; do
        is_ipv4 "$ip" || continue
        ipset add -exist "$SET" "$ip"
        found=1
      done < <(dig +short +time=3 +tries=2 A "$host" || true)
    done
    if [ "$found" = 0 ] && [ "$strict" = strict ]; then
      echo "init-firewall: cannot resolve $host" >&2
      exit 1
    fi
  done < "$HOSTS_FILE"
}

if [ "${1:-}" = --refresh ]; then
  resolve_hosts lenient 8
  exit 0
fi

fail_closed() {
  echo "init-firewall: FAILED, leaving egress blocked" >&2
  iptables -P OUTPUT DROP 2>/dev/null || true
  iptables -P INPUT DROP 2>/dev/null || true
  ip6tables -P OUTPUT DROP 2>/dev/null || true
}
trap 'fail_closed' ERR

# Reset only the filter table; Docker's embedded DNS (127.0.0.11) lives in the nat table, untouched.
iptables -F
iptables -X
ipset destroy "$SET" 2>/dev/null || true
ipset create "$SET" hash:net

echo "init-firewall: fetching GitHub ranges"
meta="$(curl -fsS --max-time 20 https://api.github.com/meta)"
echo "$meta" | jq -e '.web and .api and .git' >/dev/null
while read -r cidr; do
  is_ipv4 "$cidr" || continue # IPv6 ranges are skipped; IPv6 egress is dropped below
  ipset add -exist "$SET" "$cidr"
done < <(echo "$meta" | jq -r '(.web + .api + .git)[]')

resolve_hosts strict 20

# Loopback and replies to allowed traffic.
iptables -A INPUT -i lo -j ACCEPT
iptables -A OUTPUT -o lo -j ACCEPT
iptables -A INPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT
iptables -A OUTPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT
# DNS: Docker's embedded resolver (127.0.0.11) forwards upstream from inside this namespace, so
# port 53 must leave; restrict it to private ranges so a public resolver is not reachable.
for net in 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16; do
  iptables -A OUTPUT -p udp --dport 53 -d "$net" -j ACCEPT
  iptables -A OUTPUT -p tcp --dport 53 -d "$net" -j ACCEPT
done
iptables -A OUTPUT -m set --match-set "$SET" dst -j ACCEPT
iptables -A OUTPUT -j REJECT --reject-with icmp-admin-prohibited

iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT DROP

# IPv6: allow loopback only. If the kernel has no ip6tables support, that is acceptable only when
# the container has no global IPv6 address to begin with.
if ip6tables -F 2>/dev/null; then
  ip6tables -A INPUT -i lo -j ACCEPT
  ip6tables -A OUTPUT -o lo -j ACCEPT
  ip6tables -P INPUT DROP
  ip6tables -P FORWARD DROP
  ip6tables -P OUTPUT DROP
elif ip -6 addr show scope global 2>/dev/null | awk '/inet6/ {found = 1} END {exit !found}'; then
  echo "init-firewall: global IPv6 present but ip6tables unavailable" >&2
  exit 1
fi

echo "init-firewall: self-check"
if curl -fsS --connect-timeout 5 --max-time 8 https://example.com >/dev/null 2>&1; then
  echo "init-firewall: example.com is reachable, firewall is not effective" >&2
  exit 1
fi
curl -fsS --connect-timeout 5 --max-time 15 https://api.github.com/zen >/dev/null
echo "init-firewall: ok"
