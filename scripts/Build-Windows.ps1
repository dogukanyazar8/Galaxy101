#requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('Development', 'Release', 'Acceptance')][string]$Configuration = 'Development', [switch]$FinalPackage)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$unityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unityExecutable -PathType Leaf)) { throw 'Unity 6000.6.6f1 is not installed at the expected path.' }
$lockPath = Join-Path $projectRoot 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    try {
        $lockProbe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $lockProbe.Dispose()
    } catch [IO.IOException] { throw 'This Unity project is locked by an Editor. Close its Editor before building; no process was stopped.' }
}
$directoryName = if ($FinalPackage) { 'windows-' + $Configuration.ToLowerInvariant() } else { [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8) }
$runRoot = Join-Path $projectRoot ('artifacts\builds\' + $directoryName)
if (Test-Path -LiteralPath $runRoot) { throw "Refusing to overwrite existing build directory: $runRoot" }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$outputPath = Join-Path $runRoot 'Galaxy101.exe'
$logPath = Join-Path $runRoot 'editor.log'
$startInfo = [Diagnostics.ProcessStartInfo]::new($unityExecutable)
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$unityArguments = @('-batchmode', '-quit', '-nographics', '-force-d3d11', '-projectPath', $projectRoot, '-buildTarget', 'Win64', '-executeMethod', ('Okey101.Editor.BuildWindows.' + $Configuration), '-okeyBuildPath', $outputPath, '-logFile', $logPath)
foreach ($argument in $unityArguments) { $startInfo.ArgumentList.Add($argument) }
Write-Output "Build log: $logPath"
$process = [Diagnostics.Process]::Start($startInfo)
try { $process.WaitForExit(); $exitCode = $process.ExitCode } finally { $process.Dispose() }
Write-Output "Unity exit code: $exitCode"
$reportPath = Join-Path $runRoot 'build-report.json'
if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    Get-Content -LiteralPath $logPath -Tail 40 | Write-Output
    throw "Windows build failed. Inspect $logPath"
}
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.result -ne 'Succeeded' -or $report.configuration -ne $Configuration -or $report.errors -ne 0 -or $report.backend -ne 'Mono' -or $report.graphicsAPI -ne 'Direct3D11' -or $report.productName -ne 'Galaxy 101' -or $report.productVersion -ne '0.2.0' -or $report.ruleVersion -ne '101-v1' -or -not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
    throw "Windows build verification failed. Inspect $reportPath"
}
& (Join-Path $PSScriptRoot 'Verify-DiagnosticsBuild.ps1') -BuildDirectory $runRoot -Configuration $Configuration
Write-Output "Windows $Configuration build: $outputPath"
Write-Output "Report: $reportPath"
