#!/usr/bin/env bash
# Local helper for the Products API: start, stop, health check, and authenticated list/get/write/delete.
# The bash equivalent of manage-api.ps1 (Git Bash, WSL, macOS, Linux). Needs: dotnet, curl, openssl.
#
# Development only. Tokens are HS256 JWTs signed here with the development signing key from
# src/MediatrUnionPoc.Api/appsettings.Development.json (the key the API validates with), so no
# `dotnet user-jwts` step and no restart are needed after minting one.
#
# Users:  alice, bob  plain users (sub = name, no role)
#         root        Administrator (may DELETE and may impersonate)
#         sam         Support (may impersonate only)
#
# Usage: ./manage-api.sh <command> [options]
#   start | stop | restart | status | health
#   list  [--page 1] [--page-size 10]        (alias: read)
#   get   [--id <guid>]                      (default: the product the last `write` created)
#   write [--name <name>] [--price 9.99]
#   delete [--id <guid>]                     (default id as get; default user root)
#   token
#   set-user --user root                     tokenless requests become that user (live, no restart; Development only)
#   clear-user                               turn that off again
# Options: --user alice|bob|root|sam (default alice)  --port 5233  --token-minutes 60
set -u

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$ROOT/src/MediatrUnionPoc.Api"
STATE_DIR="$ROOT/.dev"
LOG_FILE="$STATE_DIR/api.log"
LAST_ID_FILE="$STATE_DIR/last-product-id"
DEV_USER_FILE="$PROJECT/appsettings.Development.devuser.json"

command_name="${1:-status}"
[ $# -gt 0 ] && shift

user=""
port=5233
name="Widget-$(date +%H%M%S)"
price="9.99"
id=""
page=1
page_size=10
token_minutes=60

while [ $# -gt 0 ]; do
  case "$1" in
    --user) user="$2"; shift 2 ;;
    --port) port="$2"; shift 2 ;;
    --name) name="$2"; shift 2 ;;
    --price) price="$2"; shift 2 ;;
    --id) id="$2"; shift 2 ;;
    --page) page="$2"; shift 2 ;;
    --page-size) page_size="$2"; shift 2 ;;
    --token-minutes) token_minutes="$2"; shift 2 ;;
    -h|--help) sed -n '2,26p' "${BASH_SOURCE[0]}"; exit 0 ;;
    *) echo "Unknown option: $1 (see --help)" >&2; exit 2 ;;
  esac
done

base_url="http://localhost:$port"

role_for() {
  case "$1" in
    root) echo "Administrator" ;;
    sam) echo "Support" ;;
    alice | bob) echo "" ;;
    *) echo "Unknown user '$1' (use alice, bob, root or sam)" >&2; return 1 ;;
  esac
}

b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }

json_string() { # first "<key>": "<value>" in a JSON file
  sed -n "s/.*\"$1\": *\"\([^\"]*\)\".*/\1/p" "$2" | head -n 1
}

new_token() { # $1 subject, $2 role or empty
  local key issuer audience now exp role_claim header payload signature
  key="$(json_string SigningKey "$PROJECT/appsettings.Development.json")"
  issuer="$(json_string Issuer "$PROJECT/appsettings.json")"
  audience="$(json_string Audience "$PROJECT/appsettings.json")"
  now="$(date +%s)"
  exp=$((now + token_minutes * 60))
  role_claim=""
  [ -n "$2" ] && role_claim=",\"role\":\"$2\""
  header="$(printf '%s' '{"alg":"HS256","typ":"JWT"}' | b64url)"
  payload="$(printf '{"sub":"%s","jti":"%s","iss":"%s","aud":"%s","iat":%s,"nbf":%s,"exp":%s%s}' \
    "$1" "$(head -c 16 /dev/urandom | od -An -tx1 | tr -d ' \n')" "$issuer" "$audience" "$now" "$now" "$exp" "$role_claim" | b64url)"
  signature="$(printf '%s' "$header.$payload" | openssl dgst -sha256 -hmac "$key" -binary | b64url)"
  echo "$header.$payload.$signature"
}

# Prints "METHOD path -> status", the interesting headers and the body; returns 0 on a 2xx.
api() { # $1 method, $2 path, $3 token (optional), $4 body (optional)
  local method="$1" path="$2" token="${3:-}" body="${4:-}" headers_file body_file status
  headers_file="$(mktemp)"; body_file="$(mktemp)"
  local args=(-s -m 30 -X "$method" -D "$headers_file" -o "$body_file" -w '%{http_code}')
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$body" ] && args+=(-H 'Content-Type: application/json' -d "$body")
  if ! status="$(curl "${args[@]}" "$base_url$path")"; then
    echo "$method $base_url$path failed. Is the API running? Try: ./manage-api.sh start" >&2
    rm -f "$headers_file" "$body_file"
    return 1
  fi
  echo "$method $path -> $status"
  for header in ETag Location X-Total-Count Link WWW-Authenticate; do
    tr -d '\r' < "$headers_file" | grep -i "^$header:" | sed 's/^/  /'
  done
  [ -s "$body_file" ] && cat "$body_file" && echo
  LAST_BODY="$(cat "$body_file")"
  rm -f "$headers_file" "$body_file"
  [ "$status" -ge 200 ] && [ "$status" -lt 300 ]
}

is_windows() { case "$(uname -s)" in MINGW* | MSYS* | CYGWIN*) return 0 ;; *) return 1 ;; esac; }

listening_pid() {
  if is_windows; then
    netstat -ano | tr -d '\r' | awk -v p=":$port" '$1 == "TCP" && $4 == "LISTENING" && $2 ~ p"$" { print $5; exit }'
  elif command -v lsof > /dev/null 2>&1; then
    lsof -t -iTCP:"$port" -sTCP:LISTEN 2> /dev/null | head -n 1
  else
    ss -ltnp 2> /dev/null | awk -v p=":$port" '$4 ~ p"$" { match($0, /pid=[0-9]+/); print substr($0, RSTART + 4, RLENGTH - 4); exit }'
  fi
}

is_live() { curl -s -m 2 -o /dev/null -f "$base_url/health/live"; }

start_api() {
  local pid
  pid="$(listening_pid)"
  if [ -n "$pid" ]; then
    echo "Port $port is already in use (process $pid). Run './manage-api.sh stop' first."
    return 0
  fi
  mkdir -p "$STATE_DIR"
  echo "Starting the API on $base_url (log: $LOG_FILE)..."
  (
    cd "$ROOT" || exit 1
    ASPNETCORE_ENVIRONMENT=Development nohup dotnet run --project "$PROJECT" --no-launch-profile \
      --urls "$base_url" > "$LOG_FILE" 2>&1 < /dev/null &
  )
  local deadline=$((SECONDS + 120))
  while [ $SECONDS -lt $deadline ]; do
    if is_live; then
      echo "Up: $base_url (Scalar UI: $base_url/scalar)"
      return 0
    fi
    sleep 1
  done
  echo "Not healthy after 120s. See $LOG_FILE" >&2
  return 1
}

stop_api() {
  local pid
  pid="$(listening_pid)"
  if [ -z "$pid" ]; then
    echo "Nothing is listening on port $port."
    return 0
  fi
  # Kill the whole tree: `dotnet run` is the parent of the process that owns the port.
  if is_windows; then
    taskkill //T //F //PID "$pid" > /dev/null 2>&1
  else
    local parent
    parent="$(ps -o ppid= -p "$pid" 2> /dev/null | tr -d ' ')"
    kill "$pid" 2> /dev/null
    [ -n "$parent" ] && ps -o command= -p "$parent" 2> /dev/null | grep -q 'dotnet' && kill "$parent" 2> /dev/null
  fi
  sleep 1
  if [ -n "$(listening_pid)" ]; then
    echo "Port $port is still in use." >&2
    return 1
  fi
  echo "Stopped."
}

as_user() { # sets $user (default), $role and $token; $1 = default user when --user was not given
  [ -z "$user" ] && user="$1"
  role="$(role_for "$user")" || exit 2
  token="$(new_token "$user" "$role")"
  echo "As $user (${role:-no role})$2"
}

need_id() {
  if [ -z "$id" ]; then
    if [ ! -f "$LAST_ID_FILE" ]; then
      echo "'$command_name' needs --id <product guid> (no product has been created with 'write' yet)." >&2
      exit 2
    fi
    id="$(tr -d '[:space:]' < "$LAST_ID_FILE")"
    echo "No --id given; using the product the last 'write' created: $id"
  fi
}

case "$command_name" in
  start) start_api ;;
  stop) stop_api ;;
  restart) stop_api; start_api ;;
  status)
    pid="$(listening_pid)"
    if [ -n "$pid" ]; then
      live=false; is_live && live=true
      echo "Listening on $port, process $pid. Live: $live"
    else
      echo "Not running on port $port."
    fi
    ;;
  health)
    api GET /health/live
    api GET /health/ready
    ;;
  token)
    as_user alice "" > /dev/null
    echo "$token"
    ;;
  set-user)
    if [ -z "$user" ]; then echo "'set-user' needs --user alice|bob|root|sam." >&2; exit 2; fi
    role="$(role_for "$user")" || exit 2
    roles_json="null"
    [ -n "$role" ] && roles_json="[\"$role\"]"
    printf '{
  "Authentication": {
    "DevIdentity": { "UserId": "%s", "Roles": %s }
  }
}
' "$user" "$roles_json" > "$DEV_USER_FILE"
    echo "Development identity set: tokenless requests are now $user (${role:-no role})."
    echo "A running API picks it up on the next request; a stopped one when it starts. Undo with: ./manage-api.sh clear-user"
    ;;
  clear-user)
    if [ -f "$DEV_USER_FILE" ]; then
      rm -f "$DEV_USER_FILE"
      echo "Development identity cleared: tokenless requests are refused (401) again."
    else
      echo "No development identity was set by this script."
    fi
    ;;
  list | read)
    as_user alice ", page $page of size $page_size"
    api GET "/api/v1/products?pageNumber=$page&pageSize=$page_size" "$token"
    ;;
  get)
    need_id
    as_user alice ""
    api GET "/api/v1/products/$id" "$token"
    ;;
  write)
    as_user alice "; the owner of the new product is the caller"
    LAST_BODY=""
    if api POST /api/v1/products "$token" "{\"name\":\"$name\",\"price\":$price}"; then
      created_id="$(printf '%s' "$LAST_BODY" | sed -n 's/.*"id":"\([^"]*\)".*/\1/p')"
      if [ -n "$created_id" ]; then
        mkdir -p "$STATE_DIR"
        printf '%s' "$created_id" > "$LAST_ID_FILE"
        echo "  (saved id $created_id as the default for 'get' and 'delete')"
      fi
    fi
    ;;
  delete)
    need_id
    as_user root "; DELETE needs the Administrator role"
    api DELETE "/api/v1/products/$id" "$token"
    ;;
  *)
    echo "Unknown command '$command_name'. Commands: start stop restart status health list get write delete token set-user clear-user" >&2
    exit 2
    ;;
esac
