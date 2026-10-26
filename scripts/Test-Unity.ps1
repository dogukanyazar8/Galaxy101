#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('EditMode', 'PlayMode')][string]$Platform = 'EditMode',
    [string]$Filter,
    [string]$Category = '!Simulation',
    [switch]$Graphics
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Unity-ProcessLock.ps1')
$operationMutex = Enter-GalaxyUnityProcess
try {
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$unityExecutable = 'C:\Program Files\Unity\Hub\Editor\6000.6.6f1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unityExecutable -PathType Leaf)) { throw 'Unity 6000.6.6f1 is not installed at the expected path.' }
$lockPath = Join-Path $projectRoot 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockPath) {
    try {
        $lockProbe = [IO.File]::Open($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $lockProbe.Dispose()
    } catch [IO.IOException] {
        throw 'This Unity project is locked by an Editor. Close its Editor before running batch tests; no process was stopped.'
    }
}
$runRoot = Join-Path $projectRoot ('artifacts\test-results\' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
$resultPath = Join-Path $runRoot 'results.xml'
$logPath = Join-Path $runRoot 'editor.log'
$unityArguments = @('-batchmode', '-runTests', '-projectPath', $projectRoot, '-testPlatform', $Platform, '-testResults', $resultPath, '-logFile', $logPath)
if ($Platform -eq 'EditMode' -and -not $Graphics) { $unityArguments += '-nographics' }
if ($Graphics) { $unityArguments += '-force-d3d11' }
if ($Filter) { $unityArguments += @('-testFilter', $Filter) }
if ($Category) { $unityArguments += @('-testCategory', $Category) }
$startInfo = [Diagnostics.ProcessStartInfo]::new($unityExecutable)
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
foreach ($argument in $unityArguments) { $startInfo.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($startInfo)
try {
    $process.WaitForExit()
    $exitCode = $process.ExitCode
} finally { $process.Dispose() }
Write-Output "Unity exit code: $exitCode"
Write-Output "Editor log: $logPath"
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 35 | Write-Output }
    throw "Unity produced no NUnit result. Inspect the log for compilation, license, import or test discovery failures: $logPath"
}
$xmlSettings = [Xml.XmlReaderSettings]::new()
$xmlSettings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
$xmlSettings.XmlResolver = $null
$reader = [Xml.XmlReader]::Create($resultPath, $xmlSettings)
$document = [Xml.XmlDocument]::new()
$document.XmlResolver = $null
try { $document.Load($reader) } finally { $reader.Dispose() }
$run = $document.DocumentElement
$total = [int]$run.GetAttribute('total')
$passed = [int]$run.GetAttribute('passed')
$failed = [int]$run.GetAttribute('failed')
$skipped = [int]$run.GetAttribute('skipped')
$inconclusive = [int]$run.GetAttribute('inconclusive')
Write-Output "NUnit: total=$total passed=$passed failed=$failed skipped=$skipped inconclusive=$inconclusive"
Write-Output "Results: $resultPath"
foreach ($failure in $document.SelectNodes('//test-case[@result="Failed"]')) {
    Write-Output ("FAIL: " + $failure.GetAttribute('fullname'))
    $failureMessage = $failure.SelectSingleNode('failure/message')
    if ($failureMessage) { Write-Output $failureMessage.InnerText }
}
if ($exitCode -ne 0 -or $total -le 0 -or $failed -ne 0 -or $skipped -ne 0 -or $inconclusive -ne 0 -or $passed -ne $total) {
    throw "Unity test verification failed. Results: $resultPath"
}

} finally { $operationMutex.ReleaseMutex(); $operationMutex.Dispose() }
