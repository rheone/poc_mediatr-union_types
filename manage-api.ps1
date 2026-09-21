<#
.SYNOPSIS
    Local helper for the Products API: start, stop, health check, and an authenticated read and write.

.DESCRIPTION
    Development only. Tokens are HS256 JWTs signed here with the development signing key from
    src/MediatrUnionPoc.Api/appsettings.Development.json (the same key the API validates with), so no
    `dotnet user-jwts` step and no restart are needed after minting one.

    Users:  alice, bob  plain users (sub = name, no role)
            root        Administrator (may DELETE and may impersonate)
            sam         Support (may impersonate only)

.EXAMPLE
    ./manage-api.ps1 start
    ./manage-api.ps1 health
    ./manage-api.ps1 list -User alice -Page 2 -PageSize 5
    ./manage-api.ps1 get -Id <guid> -User alice
    ./manage-api.ps1 write -User bob -Name "Gadget" -Price 12.5
    ./manage-api.ps1 delete -Id <guid>        # DELETE needs Administrator, so -User defaults to root
    ./manage-api.ps1 token -User root
    ./manage-api.ps1 set-user -User root      # tokenless requests become root (live, no restart; Development only)
    ./manage-api.ps1 clear-user               # turn that off again
    ./manage-api.ps1 stop
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('start', 'stop', 'restart', 'status', 'health', 'list', 'read', 'get', 'write', 'delete', 'token', 'set-user', 'clear-user')]
    [string]$Command = 'status',

    [ValidateSet('alice', 'bob', 'root', 'sam')]
    [string]$User = 'alice',

    [string]$Id,   # defaults to the product the last `write` created

    [int]$Page = 1,

    [ValidateRange(1, 100)]
    [int]$PageSize = 10,

    [int]$Port = 5233,

    [string]$Name = "Widget-$(Get-Date -Format 'HHmmss')",

    [decimal]$Price = 9.99,

    [int]$TokenMinutes = 60
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http   # not loaded by default in Windows PowerShell 5.1
$root = $PSScriptRoot
$project = Join-Path $root 'src/MediatrUnionPoc.Api'
$stateDir = Join-Path $root '.dev'
$logFile = Join-Path $stateDir 'api.log'
$lastIdFile = Join-Path $stateDir 'last-product-id'
$devUserFile = Join-Path $project 'appsettings.Development.devuser.json'
$baseUrl = "http://localhost:$Port"

$roles = @{ alice = @(); bob = @(); root = @('Administrator'); sam = @('Support') }

function ConvertTo-Base64Url([byte[]]$Bytes) {
    [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-DevToken([string]$Subject, [string[]]$Roles) {
    $settings = Get-Content (Join-Path $project 'appsettings.Development.json') -Raw | ConvertFrom-Json
    $key = $settings.Authentication.Jwt.SigningKey
    $base = Get-Content (Join-Path $project 'appsettings.json') -Raw | ConvertFrom-Json
    $now = [DateTimeOffset]::UtcNow

    $claims = [ordered]@{
        sub = $Subject
        jti = [guid]::NewGuid().ToString('N')
        iss = $base.Authentication.Jwt.Issuer
        aud = $base.Authentication.Jwt.Audience
        iat = $now.ToUnixTimeSeconds()
        nbf = $now.ToUnixTimeSeconds()
        exp = $now.AddMinutes($TokenMinutes).ToUnixTimeSeconds()
    }
    if ($Roles.Count -eq 1) { $claims['role'] = $Roles[0] }
    elseif ($Roles.Count -gt 1) { $claims['role'] = $Roles }

    $utf8 = [Text.Encoding]::UTF8
    $header = ConvertTo-Base64Url $utf8.GetBytes('{"alg":"HS256","typ":"JWT"}')
    $payload = ConvertTo-Base64Url $utf8.GetBytes(($claims | ConvertTo-Json -Compress))
    $hmac = [Security.Cryptography.HMACSHA256]::new($utf8.GetBytes($key))
    $signature = ConvertTo-Base64Url $hmac.ComputeHash($utf8.GetBytes("$header.$payload"))
    "$header.$payload.$signature"
}

# HttpClient behaves the same on Windows PowerShell 5.1 and PowerShell 7 and does not throw on 4xx/5xx.
function Invoke-Api([string]$Method, [string]$Path, [string]$Token, [string]$Body) {
    $client = [Net.Http.HttpClient]::new()
    try {
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), "$baseUrl$Path")
        if ($Token) { $request.Headers.TryAddWithoutValidation('Authorization', "Bearer $Token") | Out-Null }
        if ($Body) { $request.Content = [Net.Http.StringContent]::new($Body, [Text.Encoding]::UTF8, 'application/json') }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Write-Host "$Method $Path -> $([int]$response.StatusCode) $($response.ReasonPhrase)" -ForegroundColor $(if ($response.IsSuccessStatusCode) { 'Green' } else { 'Red' })
        foreach ($name in 'ETag', 'Location', 'X-Total-Count', 'Link', 'WWW-Authenticate') {
            $values = $null
            if ($response.Headers.TryGetValues($name, [ref]$values)) { Write-Host "  ${name}: $($values -join ', ')" }
        }
        $script:LastBody = $content
        if ($content) { Write-Host $content }
        return $response.IsSuccessStatusCode
    }
    catch {
        Write-Host "$Method $baseUrl$Path failed: $($_.Exception.Message). Is the API running? Try: ./manage-api.ps1 start" -ForegroundColor Red
        return $false
    }
    finally { $client.Dispose() }
}

function Get-Listener {
    Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
}

function Test-Live {
    try {
        $client = [Net.Http.HttpClient]::new()
        $client.Timeout = [TimeSpan]::FromSeconds(2)
        return $client.GetAsync("$baseUrl/health/live").GetAwaiter().GetResult().IsSuccessStatusCode
    }
    catch { return $false }
}

function Start-Api {
    if (Get-Listener) {
        Write-Host "Port $Port is already in use (process $((Get-Listener).OwningProcess)). Run './manage-api.ps1 stop' first." -ForegroundColor Yellow
        return
    }
    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    Write-Host "Starting the API on $baseUrl (log: $logFile)..."
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    Start-Process dotnet -ArgumentList @('run', '--project', "`"$project`"", '--no-launch-profile', '--urls', $baseUrl) `
        -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $logFile -RedirectStandardError "$logFile.err" | Out-Null

    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        if (Test-Live) { Write-Host "Up: $baseUrl (Scalar UI: $baseUrl/scalar)" -ForegroundColor Green; return }
        Start-Sleep -Seconds 1
    }
    Write-Host "Not healthy after 120s. See $logFile and $logFile.err" -ForegroundColor Red
}

function Stop-Api {
    $listener = Get-Listener
    if (-not $listener) { Write-Host "Nothing is listening on port $Port."; return }
    # taskkill /T also ends the `dotnet run` parent chain so nothing is left holding the build output.
    $owner = $listener.OwningProcess
    $parent = (Get-CimInstance Win32_Process -Filter "ProcessId = $owner" -ErrorAction SilentlyContinue).ParentProcessId
    foreach ($id in @($parent, $owner)) {
        $p = if ($id) { Get-CimInstance Win32_Process -Filter "ProcessId = $id" -ErrorAction SilentlyContinue }
        if ($p -and $p.CommandLine -match 'MediatrUnionPoc\.Api|dotnet') { & taskkill /T /F /PID $id | Out-Null }
    }
    Start-Sleep -Seconds 1
    if (Get-Listener) { Write-Host "Port $Port is still in use." -ForegroundColor Red } else { Write-Host "Stopped." -ForegroundColor Green }
}

$script:UserBound = $PSBoundParameters.ContainsKey('User')
if ($Command -in 'get', 'delete' -and -not $Id) {
    if (-not (Test-Path $lastIdFile)) { throw "'$Command' needs -Id <product guid> (no product has been created with 'write' yet)." }
    $Id = (Get-Content $lastIdFile -Raw).Trim()
    Write-Host "No -Id given; using the product the last 'write' created: $Id"
}
if ($Command -eq 'delete' -and -not $script:UserBound) { $User = 'root' }

switch ($Command) {
    'start' { Start-Api }
    'stop' { Stop-Api }
    'restart' { Stop-Api; Start-Api }
    'status' {
        $listener = Get-Listener
        if ($listener) { Write-Host "Listening on $Port, process $($listener.OwningProcess). Live: $(Test-Live)" } else { Write-Host "Not running on port $Port." }
    }
    'health' {
        Invoke-Api GET '/health/live' | Out-Null
        Invoke-Api GET '/health/ready' | Out-Null
    }
    'token' { New-DevToken $User $roles[$User] }
    'set-user' {
        if (-not $script:UserBound) { throw "'set-user' needs -User alice|bob|root|sam." }
        $identity = [ordered]@{ UserId = $User; Roles = @($roles[$User]) }
        if (-not $roles[$User].Count) { $identity.Roles = $null }
        @{ Authentication = @{ DevIdentity = $identity } } | ConvertTo-Json -Depth 5 |
            Set-Content -Path $devUserFile -Encoding UTF8
        Write-Host "Development identity set: tokenless requests are now $User ($(if ($roles[$User].Count) { $roles[$User] -join ',' } else { 'no role' }))." -ForegroundColor Green
        Write-Host "A running API picks it up on the next request; a stopped one when it starts. Undo with: ./manage-api.ps1 clear-user"
    }
    'clear-user' {
        if (Test-Path $devUserFile) { Remove-Item $devUserFile; Write-Host "Development identity cleared: tokenless requests are refused (401) again." -ForegroundColor Green }
        else { Write-Host "No development identity was set by this script." }
    }
    { $_ -in 'list', 'read' } {
        $token = New-DevToken $User $roles[$User]
        Write-Host "As $User ($(if ($roles[$User].Count) { $roles[$User] -join ',' } else { 'no role' })), page $Page of size $PageSize"
        Invoke-Api GET "/api/v1/products?pageNumber=$Page&pageSize=$PageSize" $token | Out-Null
    }
    'get' {
        $token = New-DevToken $User $roles[$User]
        Write-Host "As $User ($(if ($roles[$User].Count) { $roles[$User] -join ',' } else { 'no role' }))"
        Invoke-Api GET "/api/v1/products/$Id" $token | Out-Null
    }
    'delete' {
        $token = New-DevToken $User $roles[$User]
        Write-Host "As $User ($(if ($roles[$User].Count) { $roles[$User] -join ',' } else { 'no role' })); DELETE needs the Administrator role"
        Invoke-Api DELETE "/api/v1/products/$Id" $token | Out-Null
    }
    'write' {
        $token = New-DevToken $User $roles[$User]
        Write-Host "As $User ($(if ($roles[$User].Count) { $roles[$User] -join ',' } else { 'no role' })); the owner of the new product is '$User'"
        $body = @{ name = $Name; price = $Price } | ConvertTo-Json -Compress
        $script:LastBody = $null
        if (Invoke-Api POST '/api/v1/products' $token $body) {
            $created = $script:LastBody | ConvertFrom-Json
            New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
            Set-Content -Path $lastIdFile -Value $created.id -NoNewline
            Write-Host "  (saved id $($created.id) as the default for 'get' and 'delete')"
        }
    }
}
