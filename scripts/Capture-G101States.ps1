#requires -Version 7.0
# v4 state router capture (docs/v4/KARARLAR.md K-06): runs a Development player windowed at 1280x720 with
# -g101State and collects <ID>.png, <ID>.layout.json, <ID>.text.json and manifest.json in -OutputDirectory.
# Without -BuildDirectory it builds a fresh Development player first (scripts/Build-Windows.ps1).
[CmdletBinding()]
param(
    [string]$BuildDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$States = 'all',
    [ValidateSet('saturn', 'mars', 'neptun', 'jupiter', 'venus')][string]$Theme,
    [ValidateSet('tr', 'en')][string]$Lang,
    [int]$Seed = 42,
    [int]$Ft = -1,
    [int]$TimeoutSeconds = 3600,
    [string]$Size = '1280x720',
    [double]$FpsSeconds = 0
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $BuildDirectory) {
    $buildOutput = & (Join-Path $PSScriptRoot 'Build-Windows.ps1') -Configuration Development
    $buildOutput | Write-Output
    $BuildDirectory = Split-Path -Parent (($buildOutput | Select-String 'Build log: (.+)$').Matches[0].Groups[1].Value)
}
$buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
$report = Get-Content -LiteralPath (Join-Path $buildRoot 'build-report.json') -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded' -or $report.configuration -ne 'Development') { throw 'The state router needs a successful Development build.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$executable = Join-Path $buildRoot ([IO.Path]::GetFileName($report.executable))
$info = [Diagnostics.ProcessStartInfo]::new($executable)
$info.UseShellExecute = $false
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$w, $h = $Size -split 'x'
$arguments = @('-screen-width', $w, '-screen-height', $h, '-screen-fullscreen', '0', '-popupwindow', '-force-d3d11',
    "-g101State=$States", "-g101Seed=$Seed", "-g101Out=$outputRoot", '-logFile', (Join-Path $outputRoot 'player.log'))
if ($Theme) { $arguments += "-g101Theme=$Theme" }
if ($Lang) { $arguments += "-g101Lang=$Lang" }
if ($Ft -ge 0) { $arguments += "-g101Ft=$Ft" }
$arguments += "-g101Size=$Size"
if ($FpsSeconds -gt 0) { $arguments += "-g101Fps=$FpsSeconds" }
foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($info)
$null = $process.StandardOutput.ReadToEndAsync(); $null = $process.StandardError.ReadToEndAsync()   # the player log has it all
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill($true); $process.WaitForExit(); throw "State capture exceeded $TimeoutSeconds s." }
    $exit = $process.ExitCode
} finally { if (-not $process.HasExited) { $process.Kill($true) }; $process.Dispose() }
$manifestPath = Join-Path $outputRoot 'manifest.json'
if ($exit -ne 0 -or -not (Test-Path -LiteralPath $manifestPath)) { Get-Content -LiteralPath (Join-Path $outputRoot 'player.log') -Tail 40 | Write-Output; throw "State capture failed: exit $exit" }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$failed = @($manifest.states | Where-Object { $_.error })
Write-Output ("Captured {0} states ({1} with a path error) in {2}" -f $manifest.states.Count, $failed.Count, $outputRoot)
foreach ($f in $failed) { Write-Output ("  {0}: {1}" -f $f.id, $f.error) }
