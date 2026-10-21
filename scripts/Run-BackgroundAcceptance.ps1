#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Smoke','Flow','Soak')][string]$Mode='Smoke'
)
$ErrorActionPreference='Stop'
$buildRoot=[IO.Path]::GetFullPath($BuildDirectory)
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
$report=Get-Content -LiteralPath (Join-Path $buildRoot 'build-report.json') -Raw | ConvertFrom-Json
if($report.result -ne 'Succeeded' -or $report.configuration -ne 'Acceptance' -or $report.graphicsAPI -ne 'Direct3D11'){throw 'Requires a successful DX11 Acceptance package.'}
$boot=Get-Content -LiteralPath (Join-Path $buildRoot ([IO.Path]::GetFileNameWithoutExtension($report.executable) + '_Data\boot.config'))
if(@($boot|Where-Object {$_ -match '^player-connection-'}).Count -gt 0){throw 'Refusing a background package with PlayerConnection configuration.'}
if(Test-Path -LiteralPath $outputRoot){throw 'Refusing to overwrite an acceptance run.'}
$dataRoot=Join-Path $outputRoot 'evidence'
New-Item -ItemType Directory -Path $dataRoot -Force|Out-Null
$info=[Diagnostics.ProcessStartInfo]::new((Join-Path $buildRoot ([IO.Path]::GetFileName($report.executable))))
$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$argsList=@('-batchmode','-force-d3d11','-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-okeyA4Background','-galaxyMenuState',(Join-Path $dataRoot 'galaxy-menu.json'),'-logFile',(Join-Path $outputRoot 'player.log'))
if($Mode -eq 'Flow'){$argsList+=@('-okeyA4FlowOutput',$dataRoot)}else{$argsList+=@('-okeyA4Output',$dataRoot)}
if($Mode -eq 'Smoke'){$argsList+='-okeyA4Smoke'}
foreach($argument in $argsList){$info.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($info)
$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
$watch=[Diagnostics.Stopwatch]::StartNew()
$timeout=if($Mode -eq 'Soak'){1875}else{180}
$systemPath=Join-Path $outputRoot 'system-load.csv'
'second,totalCpuPercent,availableMemoryMB,playerCpuSeconds,playerWorkingSetBytes'|Set-Content -LiteralPath $systemPath
$timedOut=$false
try {
    $process.PriorityClass=[Diagnostics.ProcessPriorityClass]::BelowNormal
    @{pid=$process.Id;mode=$Mode;startedUtc=[DateTime]::UtcNow.ToString('O');buildDirectory=$buildRoot;priority='BelowNormal';desktopInputUsed=$false;rendering='DX11 offscreen 1920x1080';audio='muted';conditions='User continues desktop work; foreground acceptance is separate.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $outputRoot 'run.json')
    Write-Output "Background $Mode PID=$($process.Id); evidence=$dataRoot"
    while(-not $process.WaitForExit(15000)) {
        $process.Refresh()
        $cpu=Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'"
        $os=Get-CimInstance Win32_OperatingSystem
        [string]::Format([Globalization.CultureInfo]::InvariantCulture,'{0:F3},{1},{2:F1},{3:F3},{4}',$watch.Elapsed.TotalSeconds,$cpu.PercentProcessorTime,($os.FreePhysicalMemory/1024),$process.TotalProcessorTime.TotalSeconds,$process.WorkingSet64)|Add-Content -LiteralPath $systemPath
        if($watch.Elapsed.TotalSeconds -gt $timeout){$timedOut=$true;$process.Kill($true);$process.WaitForExit();break}
    }
    $exitCode=$process.ExitCode
    $stdout.GetAwaiter().GetResult()|Set-Content -LiteralPath (Join-Path $outputRoot 'stdout.log')
    $stderr.GetAwaiter().GetResult()|Set-Content -LiteralPath (Join-Path $outputRoot 'stderr.log')
    @{exitCode=$exitCode;timedOut=$timedOut;elapsedSeconds=$watch.Elapsed.TotalSeconds;completedUtc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $outputRoot 'process-result.json')
    if($timedOut){throw 'Owned background acceptance process exceeded its deadline; it was stopped and evidence retained.'}
    $resultFile=Join-Path $dataRoot $(if($Mode -eq 'Flow'){'flow.json'}else{'soak.json'})
    if($exitCode -ne 0 -or -not(Test-Path -LiteralPath $resultFile)){throw "Background player failed: exit=$exitCode; inspect $outputRoot"}
    $result=Get-Content -LiteralPath $resultFile -Raw|ConvertFrom-Json
    if(-not $result.completed){throw "Background acceptance did not complete: $($result.failure)"}
    Write-Output "Background $Mode completed: $resultFile"
} finally {
    if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()}
    $process.Dispose()
}
