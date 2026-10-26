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
# A whole-catalogue run repeatedly loads scenes. Keep each player lifetime bounded after the interrupted run13.
$stateIds = @($States -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($stateIds.Count -lt 1 -or $stateIds.Count -gt 4 -or $stateIds -contains 'all') {
    throw "Capture at most 4 explicit state IDs per run; 'all' is disabled. Use sequential small batches."
}
if (@($stateIds | Select-Object -Unique).Count -ne $stateIds.Count) { throw 'State IDs must be unique.' }
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $outputRoot) -and @(Get-ChildItem -LiteralPath $outputRoot -Force).Count -gt 0) {
    throw 'The capture output directory must be empty; use a fresh directory for every batch.'
}
$runningUnity = @(Get-Process -Name Unity, Galaxy101 -ErrorAction SilentlyContinue)
if ($runningUnity.Count -gt 0) { throw 'A Unity editor or Galaxy101 player is already running. Close it before capture; no process was stopped.' }
if (-not $BuildDirectory) {
    $buildOutput = & (Join-Path $PSScriptRoot 'Build-Windows.ps1') -Configuration Development
    $buildOutput | Write-Output
    $BuildDirectory = Split-Path -Parent (($buildOutput | Select-String 'Build log: (.+)$').Matches[0].Groups[1].Value)
}
$buildRoot = [IO.Path]::GetFullPath($BuildDirectory)
$report = Get-Content -LiteralPath (Join-Path $buildRoot 'build-report.json') -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded' -or $report.configuration -ne 'Development') { throw 'The state router needs a successful Development build.' }
if ($report.captureWindowGuardVersion -ne 1) { throw 'This build lacks the capture window guard; build a fresh Development player before capture.' }
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$executable = Join-Path $buildRoot ([IO.Path]::GetFileName($report.executable))
$info = [Diagnostics.ProcessStartInfo]::new($executable)
$info.UseShellExecute = $false
$info.RedirectStandardOutput = $true
$info.RedirectStandardError = $true
$w, $h = $Size -split 'x'
$arguments = @('-screen-width', $w, '-screen-height', $h, '-screen-fullscreen', '0', '-popupwindow', '-force-d3d11',
    "-g101State=$($stateIds -join ',')", "-g101Seed=$Seed", "-g101Out=$outputRoot", '-logFile', (Join-Path $outputRoot 'player.log'))
if ($Theme) { $arguments += "-g101Theme=$Theme" }
if ($Lang) { $arguments += "-g101Lang=$Lang" }
if ($Ft -ge 0) { $arguments += "-g101Ft=$Ft" }
$arguments += "-g101Size=$Size"
if ($FpsSeconds -gt 0) { $arguments += "-g101Fps=$FpsSeconds" }
foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($info)
$null = $process.StandardOutput.ReadToEndAsync(); $null = $process.StandardError.ReadToEndAsync()   # the player log has it all
$startedUtc = [DateTime]::UtcNow
$peakWorkingSet = 0L; $peakPrivateBytes = 0L
try {
    while (-not $process.WaitForExit(1000)) {
        $process.Refresh()
        $peakWorkingSet = [Math]::Max($peakWorkingSet, $process.WorkingSet64)
        $peakPrivateBytes = [Math]::Max($peakPrivateBytes, $process.PrivateMemorySize64)
        if (([DateTime]::UtcNow - $startedUtc).TotalSeconds -gt $TimeoutSeconds) {
            $process.Kill($true); $process.WaitForExit(); throw "State capture exceeded $TimeoutSeconds s."
        }
    }
    $exit = $process.ExitCode
} finally { if (-not $process.HasExited) { $process.Kill($true) }; $process.Dispose() }
@{ startedUtc = $startedUtc.ToString('o'); finishedUtc = [DateTime]::UtcNow.ToString('o'); states = $stateIds; size = $Size; exitCode = $exit; peakWorkingSetBytes = $peakWorkingSet; peakPrivateBytes = $peakPrivateBytes } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputRoot 'process.json')
$manifestPath = Join-Path $outputRoot 'manifest.json'
if ($exit -ne 0 -or -not (Test-Path -LiteralPath $manifestPath)) { Get-Content -LiteralPath (Join-Path $outputRoot 'player.log') -Tail 40 | Write-Output; throw "State capture failed: exit $exit" }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$failed = @($manifest.states | Where-Object { $_.error })
Write-Output ("Captured {0} states ({1} with a path error) in {2}" -f $manifest.states.Count, $failed.Count, $outputRoot)
foreach ($f in $failed) { Write-Output ("  {0}: {1}" -f $f.id, $f.error) }
if ($failed.Count -gt 0 -or (($manifest.states.id -join ',') -cne ($stateIds -join ','))) { throw 'Capture did not complete every requested state successfully.' }
foreach ($id in $stateIds) {
    foreach ($suffix in @('.png', '.layout.json', '.text.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $outputRoot ($id + $suffix)) -PathType Leaf)) { throw "Capture is missing $id$suffix." }
    }
}
