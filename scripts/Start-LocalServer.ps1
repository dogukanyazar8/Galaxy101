#requires -Version 7.0
<#
.SYNOPSIS
Runs the Galaxy 101 game server on this machine for a full local test (the Windows build connects to it by default).

.DESCRIPTION
1. Starts (or creates) the PostgreSQL 17 container "galaxy101-dev-pg" on port 5433 (Docker Desktop must be running).
2. Keeps a signing key in Server\.devkey (created once, git-ignored) so sign-ins survive restarts.
3. Runs the server at http://localhost:5173 until you press Ctrl+C. Migrations apply on startup.

.PARAMETER Port
HTTP port (default 5173, what the game uses unless started with -galaxyServer <url>).

.PARAMETER Fresh
Delete and recreate the database container first (all accounts, chips and matches are wiped).
#>
[CmdletBinding()]
param([int]$Port = 5173, [switch]$Fresh)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$container = 'galaxy101-dev-pg'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Docker is not installed or not on PATH. Install Docker Desktop and start it.' }
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker Desktop is not running. Start it and run this script again.' }

if ($Fresh) { docker rm -f $container *> $null }
$state = docker ps -a --filter "name=^$container$" --format '{{.State}}'
if (-not $state) {
    Write-Output "Creating PostgreSQL container $container on port 5433..."
    docker run -d --name $container -e POSTGRES_PASSWORD=dev -p 5433:5432 postgres:17-alpine | Out-Null
} elseif ($state -ne 'running') {
    Write-Output "Starting PostgreSQL container $container..."
    docker start $container | Out-Null
}
# Wait until the database accepts connections.
for ($i = 0; $i -lt 30; $i++) {
    docker exec $container pg_isready -U postgres *> $null
    if ($LASTEXITCODE -eq 0) { break }
    Start-Sleep -Seconds 1
}
if ($LASTEXITCODE -ne 0) { throw "PostgreSQL in $container did not become ready." }

$keyPath = Join-Path $root 'Server\.devkey'
if (-not (Test-Path -LiteralPath $keyPath)) {
    $bytes = [byte[]]::new(48); [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    Set-Content -LiteralPath $keyPath -Value ([Convert]::ToBase64String($bytes)) -NoNewline
}
$key = (Get-Content -LiteralPath $keyPath -Raw).Trim()

$busy = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($busy) { throw "Port $Port is already in use (process $($busy[0].OwningProcess)). Stop that server first or pass -Port." }

Write-Output "Galaxy 101 server: http://localhost:$Port  (Ctrl+C to stop)"
Write-Output "Health check: http://localhost:$Port/health"
Set-Location (Join-Path $root 'Server')
dotnet run --project src/Galaxy101.Server --urls "http://localhost:$Port" `
    --ConnectionStrings:Game "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=dev" `
    --Auth:SigningKey $key
