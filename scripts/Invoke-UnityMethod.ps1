#requires -Version 7.0
# Runs one static editor method in batch mode against this project and returns Unity's exit code.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Method, [string]$LogPath)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe'
if (-not $LogPath) { $LogPath = Join-Path $projectRoot ('artifacts\logs\' + $Method + '-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmss') + '.log') }
New-Item -ItemType Directory -Force -Path (Split-Path $LogPath) | Out-Null
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$projectRoot`"", '-executeMethod', $Method, '-logFile', "`"$LogPath`"") -NoNewWindow -PassThru -Wait
Write-Output "Log: $LogPath"
Write-Output "Exit: $($p.ExitCode)"
exit $p.ExitCode
