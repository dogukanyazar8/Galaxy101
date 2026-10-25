#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildDirectory,[Parameter(Mandatory)][string]$OutputDirectory,[switch]$Presentation)
$ErrorActionPreference='Stop'
$buildRoot=[IO.Path]::GetFullPath($BuildDirectory)
$outputRoot=[IO.Path]::GetFullPath($OutputDirectory)
$report=Get-Content -LiteralPath (Join-Path $buildRoot 'build-report.json') -Raw | ConvertFrom-Json
if($report.result -ne 'Succeeded' -or $report.configuration -notin @('Acceptance','Development')){throw 'Capture requires a successful diagnostic package.'}
if(Test-Path -LiteralPath $outputRoot){throw "Refusing to overwrite capture evidence: $outputRoot"}
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$executable=Join-Path $buildRoot ([IO.Path]::GetFileName($report.executable))
$info=[Diagnostics.ProcessStartInfo]::new($executable)
$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
foreach($argument in @('-batchmode','-force-d3d11','-screen-width','1280','-screen-height','720','-screen-fullscreen','0','-galaxyCaptureOutput',$outputRoot,'-logFile',(Join-Path $outputRoot 'player.log'))){$info.ArgumentList.Add($argument)}
if($Presentation){$info.ArgumentList.Add('-galaxyCapturePresentation')}
$process=[Diagnostics.Process]::Start($info)
try{
    $process.PriorityClass=[Diagnostics.ProcessPriorityClass]::BelowNormal
    $deadline=[DateTime]::UtcNow.AddSeconds(270)
    while(-not $process.WaitForExit(50)){
        if([DateTime]::UtcNow -ge $deadline){$process.Kill($true);$process.WaitForExit();throw 'Owned capture player exceeded its 270-second deadline.'}
    }
    if($process.ExitCode -ne 0){throw "Capture player failed: exit $($process.ExitCode). See $outputRoot/player.log"}
}finally{if(-not $process.HasExited){$process.Kill($true);$process.WaitForExit()};$process.Dispose()}
$manifest=Get-Content -LiteralPath (Join-Path $outputRoot 'manifest.json') -Raw | ConvertFrom-Json
if($manifest.status -ne 'completed' -or $manifest.captures.Count -ne 56 -or @($manifest.captures|Where-Object{-not $_.nonblank}).Count -ne 0){throw 'Capture manifest is incomplete or contains blank frames.'}
$referenceRoot=Join-Path $PSScriptRoot '../Design/old/galaxy101_unity_handoff/screenshots'
$expected=@(Get-ChildItem -LiteralPath $referenceRoot -Filter '*.png' -File|Select-Object -ExpandProperty Name)
$actual=@($manifest.captures|Where-Object reference|Select-Object -ExpandProperty file)
if(Compare-Object $expected $actual){throw 'Capture did not cover the exact supplied reference set.'}
Write-Output "Verified 54 reference states + 1920x1080/2560x1440: $outputRoot"
