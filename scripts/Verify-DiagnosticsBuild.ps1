#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildDirectory,
    [Parameter(Mandatory)][ValidateSet('Development', 'Release', 'Acceptance')][string]$Configuration
)
$ErrorActionPreference = 'Stop'
$report = Get-Content -LiteralPath (Join-Path $BuildDirectory 'build-report.json') -Raw | ConvertFrom-Json
$dataDirectoryName = [IO.Path]::GetFileNameWithoutExtension($report.executable) + '_Data'
$dataPath = Join-Path ([IO.Path]::GetFullPath($BuildDirectory)) $dataDirectoryName
function Read-TypeMethods([string]$assemblyPath, [string]$namespace, [string]$typeName) {
    $stream = [IO.File]::OpenRead($assemblyPath)
    $peReader = $null
    try {
        $peReader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($peReader)
        foreach ($handle in $metadata.TypeDefinitions) {
            $type = $metadata.GetTypeDefinition($handle)
            if ($metadata.GetString($type.Name) -eq $typeName -and $metadata.GetString($type.Namespace) -eq $namespace) {
                foreach ($method in $type.GetMethods()) { $metadata.GetString($metadata.GetMethodDefinition($method).Name) }
            }
        }
    } finally { if ($peReader) { $peReader.Dispose() }; $stream.Dispose() }
}
$panelMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Okey101.Presentation.dll') 'Okey101.Presentation' 'DebugPanel')
$practiceMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Okey101.Application.dll') 'Okey101.Application' 'PracticeDeals')
$acceptanceMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Okey101.Presentation.dll') 'Okey101.Presentation' 'TableAcceptanceProbe')
$soakMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Okey101.Presentation.dll') 'Okey101.Presentation' 'WindowsSoakProbe')
$flowMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Okey101.Presentation.dll') 'Okey101.Presentation' 'WindowsFlowProbe')
$backgroundMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Okey101.Presentation.dll') 'Okey101.Presentation' 'BackgroundAcceptance')
$galaxyCaptureMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Galaxy101.Runtime.dll') 'Galaxy101' 'GalaxyCaptureDriver')
$isDevelopment = $Configuration -ne 'Release'
$seedMethods = @(Read-TypeMethods (Join-Path $dataPath 'Managed\Okey101.Presentation.dll') 'Okey101.Presentation' 'MainMenu')
if (($seedMethods -contains 'get_DiagnosticNextSeed') -ne $isDevelopment) { throw "Unexpected $Configuration deterministic deal-seed inclusion." }
if (($galaxyCaptureMethods -contains 'ResolveOutput') -ne $isDevelopment) { throw "Unexpected $Configuration Galaxy capture code inclusion." }
foreach ($method in @('Initialize', 'LoadScenario', 'StepBotAsync', 'ExportReplayAsync', 'ReplayAsync')) {
    if (($panelMethods -contains $method) -ne $isDevelopment) { throw "Unexpected $Configuration diagnostic method: $method" }
}
if (($practiceMethods -contains 'Create') -ne $isDevelopment) { throw "Unexpected $Configuration practice-deal code inclusion." }
if (($acceptanceMethods -contains 'Initialize') -ne $isDevelopment) { throw "Unexpected $Configuration acceptance-probe inclusion." }
if (($soakMethods -contains 'Initialize') -ne $isDevelopment) { throw "Unexpected $Configuration soak-probe inclusion." }
if (($flowMethods -contains 'RunAsync') -ne $isDevelopment -or ($backgroundMethods -contains 'Configure') -ne $isDevelopment) { throw "Unexpected $Configuration background-acceptance inclusion." }
$fixtureNames = @('series-opening', 'pair-opening', 'joker-swap', 'finish-x8', 'stock-end', 'four-pairs')
$included = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($file in Get-ChildItem -LiteralPath $dataPath -Filter '*.assets' -File) {
    $assetText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($file.FullName))
    foreach ($name in $fixtureNames) { if ($assetText.Contains($name)) { [void]$included.Add($name) } }
}
foreach ($name in $fixtureNames) {
    if ($included.Contains($name) -ne $isDevelopment) { throw "Unexpected $Configuration fixture inclusion: $name" }
}
Write-Output "Diagnostics package verified: $Configuration; fixture assets=$($included.Count)."
